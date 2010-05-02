using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using CubeHags.client.map.Source;

namespace CubeHags.client.render
{
    public interface RenderChildren
    {
        List<RenderItem> items { get; set; }
        SourceMaterial material { get; set; }
        void Render(Effect effect, Device device, bool setMaterial);
        bool SharedTexture2 { get; set; }
        bool SharedTexture1 { get; set; }
        HagsVertexBuffer vb { get; set; }
        HagsIndexBuffer ib { get; set; }
        void Dispose();
    }
}
