using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
using CubeHags.client.render;

namespace CubeHags.client.map.Source
{
    //public class RenderModel : RenderItem
    //{
    //    public Matrix modelMatrix;

    //    public RenderModel()
    //        : base(null, null)
    //    {

    //    }

    //    public new void Render(Effect effect, Device device, bool setMaterial)
    //    {
    //        // Set modelMatrix
    //        Matrix worldview = modelMatrix * Renderer.Instance.Camera.World;
    //        effect.SetValue("WorldViewProj", worldview * Renderer.Instance.Camera.Projection);

    //        foreach (RenderChildren item in items)
    //        {
    //            item.Render(effect, device, setMaterial);
    //        }
    //    }
    //}
}
