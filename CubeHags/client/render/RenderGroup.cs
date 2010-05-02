using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using CubeHags.client.map.Source;
using CubeHags.client.render;
using SlimDX.Direct3D9;
using System.Runtime.InteropServices;


namespace CubeHags.client
{
    public class RenderGroup : RenderChildren
    {
        public bool SharedTexture2 { get; set; }
        public bool SharedTexture1 { get; set; }
        public Texture tex;
        public Texture tex2;
        public int stride;
        private SourceMaterial _material = null;
        public SourceMaterial material { get { return _material; } set { _material = value; } }

        // Quake3 stuff
        public int textureId; // lightmaps or regular texture id

        public HagsVertexBuffer vb { get; set; }
        public HagsIndexBuffer ib { get; set; }
        private List<RenderItem> _items = new List<RenderItem>();
        public Mesh mesh;

        public RenderGroup()
        {
            vb = null;
            SharedTexture1 = false;
            SharedTexture2 = false;
        }

        public void Render(Effect effect, Device device, bool setMaterial)
        {
            if (SharedTexture1 && tex != null)
                device.SetTexture(0, tex);
            if (SharedTexture2 && tex2 != null)
                device.SetTexture(1, tex2);

            foreach (RenderItem item in _items)
            {
                item.Render(effect, device, setMaterial);
            }
        }

        public void Optimize()
        {
            long optimizeTime = HighResolutionTimer.Ticks;
            List<RenderItem> Optimized = new List<RenderItem>();
            foreach (RenderItem item in _items)
            {
                if (item.DontOptimize)
                {
                    Optimized.Add(item);
                }
                else
                {
                    bool similarFound = false;
                    // Try to find a similar item
                    foreach (RenderItem optItem in Optimized)
                    {
                        if (!optItem.DontOptimize && item.material.MaterialID == optItem.material.MaterialID && optItem.vb.VF == item.vb.VF)
                        {
                            optItem.SharedTexture1 = true;
                            optItem.SharedTexture2 = true;
                            item.parent = optItem;
                            if (item.face != null && optItem.face != null)
                            {
                                item.face.item = optItem;
                            }
                            optItem.items.Add(item);
                            similarFound = true;
                            break;
                        }
                    }
                    // Add to list
                    if (!similarFound)
                    {
                        Optimized.Add(item);
                    }
                }
            }
            _items = Optimized;

            optimizeTime = HighResolutionTimer.Ticks - optimizeTime;
            System.Console.WriteLine("[RenderGroup] Optimize took {0:0.00}ms", ((float)optimizeTime / (float)HighResolutionTimer.Frequency) * 1000f);
        }

        public void Init()
        {
            foreach (RenderItem item in _items)
            {
                item.GenerateIndexBuffer();
            }
        }

        public void Dispose()
        {
            foreach (RenderChildren item in items)
            {
                item.Dispose();
            }
            if (vb != null) vb.Dispose();
            if (tex != null) tex.Dispose();
            if (tex2 != null) tex2.Dispose();
        }

        #region RenderChildren Members

        public List<RenderItem> items
        {
            get
            {
                return _items;
            }
            set
            {
                _items = value;
            }
        }

        #endregion
    }
}
