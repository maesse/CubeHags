using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.client.render;

namespace CubeHags.client
{
    public class RenderItem : RenderChildren
    {
        // Options
        public bool SharedTexture2 { get; set; }
        public bool SharedTexture1 { get; set; }
        public bool DontOptimize = false;

        // Data
        private HagsVertexBuffer _vb = null;
        public HagsVertexBuffer vb { get { if (_vb == null && parent != null) return parent.vb; else return _vb; } set { _vb = value; } }
        private HagsIndexBuffer _ib = null;
        public HagsIndexBuffer ib { get { if (_ib == null && parent != null) return parent.ib; else return _ib; } set { _ib = value; } }
        public List<uint> indices = new List<uint>();
        private SourceMaterial _material = null;
        public SourceMaterial material { get { return _material; } set { _material = value; } }

        // Drawing
        public PrimitiveType Type = PrimitiveType.TriangleList;
        public RenderChildren parent;
        public int ID; // RenderItem ID

        // Info
        bool itemsOptimized = false;
        public int nVerts;
        public int lowestIndiceValue = 0;
        public int nIndices;
        public int vertexStartIndex = 0;
        public int IndiceStartIndex = 0;
        public uint IndiceMin = 0;
        public uint IndiceMax = 0;

        // Special stuff
        public int TextureID; // Used by Quake3
        public Face face = null; // Used by Source

        public RenderItem(RenderChildren parent, SourceMaterial material)
        {
            vb = null;
            SharedTexture1 = false;
            SharedTexture2 = false;
            this.parent = parent;
            this.material = material;
            if (this.material == null)
                this.material = new SourceMaterial();
            this.ID = Renderer.Instance.NextRenderItemID;
        }

        // Render this Item + all children
        public void Render(Effect effect, Device device, bool setMaterial)
        {
            if (!itemsOptimized && !DontOptimize)
            {
                List<uint> ind = new List<uint>(indices);
                int vertCount = nVerts;
                foreach (RenderItem item in items)
                {
                    ind.AddRange(item.indices);
                    vertCount += item.nVerts;
                }
                if (ind.Count == 0)
                {
                    // Abort
                    itemsOptimized = true;
                    return;
                }
                ib.SetIB<uint>(ind.ToArray(), ind.Count * sizeof(uint), Usage.WriteOnly, false);
                device.Indices = ib.IB;
                nIndices = ind.Count;
                nVerts = vertCount;
                itemsOptimized = true;
            }
            
            // Set material
            if (setMaterial)
                material.ApplyMaterial(device);

            int primCount = 0;
            switch (Type)
            {
                case PrimitiveType.TriangleList:
                    primCount = nIndices / 3;
                    break;
                case PrimitiveType.TriangleFan:
                case PrimitiveType.TriangleStrip:
                    primCount = nIndices - 2;
                    break;
            }

            if (primCount > 0)
            {
                // Draw
                device.DrawIndexedPrimitives(Type, vertexStartIndex, lowestIndiceValue, nVerts, IndiceStartIndex, primCount);
            }

            // Render children
            if (!itemsOptimized || DontOptimize)
            {
                foreach (RenderChildren children in items)
                {
                    children.Render(effect, device, false);
                }
            }
        }

        public void Init()
        {
            if (indices.Count > 0)
            {
                // Figure out nverts
                uint min = indices[0], max = indices[0];
                for (int i = 1; i < indices.Count; i++)
                {
                    if (min > indices[i])
                        min = indices[i];
                    else if (max < indices[i])
                        max = indices[i];
                }
                IndiceMax = max;
                IndiceMin = min;
                nVerts = (int)(max - min) + 1;
                lowestIndiceValue = (int)min;
                nIndices = indices.Count;
            }
        }

        // Generates IB from an Indices List
        public bool GenerateIndexBuffer()
        {
            if (indices.Count > 0)
            {
                int nBytes = indices.Count * sizeof(uint);
                if (ib == null)
                    ib = new HagsIndexBuffer();
                
                    ib.SetIB<uint>(indices.ToArray(), nBytes, Usage.WriteOnly, false);

                // Figure out nverts
                uint min = indices[0], max = indices[0];
                for (int i = 1; i < indices.Count; i++)
                {
                    if (min > indices[i])
                        min = indices[i];
                    else if(max < indices[i])
                        max = indices[i];
                }

                nVerts = (int)(max - min)+1;
                lowestIndiceValue = (int)min;
                nIndices = indices.Count;
                return true;
            }
            return false;
        }

        public static int GetLowestIndiceValue(IEnumerable<uint> col)
        {
            uint lowest = 99999;
            foreach (uint val in col)
                if (lowest == 99999 || lowest > val)
                    lowest = val;
            return (int)lowest;
        }


        public bool CompareBase(RenderItem other)
        {
            if (material.baseTexture == other.material.baseTexture)
            {
                return true;
            }
            return false;
        }

        public bool CompareBump(RenderItem other)
        {
            if (material.baseTexture == other.material.baseTexture)
            {
                if (material.Bumpmap == other.material.Bumpmap)
                {
                    if (material.bumpmapTexture == other.material.bumpmapTexture)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Dispose()
        {
            // Dispose children
            foreach (RenderItem child in items)
            {
                child.Dispose();
            }

            // Dispose this
            if (ib != null)
                ib.Dispose();
            if (vb != null)
                vb.Dispose();
            if (material != null)
                material.Dispose();
        }

        private List<RenderItem> _items = new List<RenderItem>();
        public List<RenderItem> items
        {
            get
            {
                return _items;
            }
            set
            {
                _items = value;
                itemsOptimized = false;
            }
        }
    }
}
