using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.client.render.Formats;

namespace CubeHags.client
{
    public class MegaTexture
    {
        private Device device;
        private Texture simple;
        RenderGroup group = new RenderGroup();

        public MegaTexture(Device device)
        {
            this.device = device;
            simple = TextureManager.Instance.LoadTexture("client/gfx/heightmaps/heightmap.dds");
            //simple = TextureManager.Instance.LoadTexture("client/gfx/uvDetective_512.dds");
            

            // Create plane w/ tex-coords
            VertexPositionNormalTextured[] vert = new VertexPositionNormalTextured[6];
            //vert[0].X = -100.0f; vert[0].Y = 100.0f; vert[0].Z = 1.0f;
            //vert[1].X = 100.0f; vert[1].Y = 100.0f; vert[1].Z = 1.0f;
            //vert[2].X = -100.0f; vert[2].Y = -100.0f; vert[2].Z = 1.0f;
            //vert[3].X = -100.0f; vert[3].Y = -100.0f; vert[3].Z = 1.0f;
            //vert[4].X = 100.0f; vert[4].Y = 100.0f; vert[4].Z = 1.0f;
            //vert[5].X = 100.0f; vert[5].Y = -100.0f; vert[5].Z = 1.0f;
            //vert[0].Tu = 0.0f; vert[0].Tv = 0.0f;
            //vert[1].Tu = 1.0f; vert[1].Tv = 0.0f;
            //vert[2].Tu = 0.0f; vert[2].Tv = 1.0f;
            //vert[3].Tu = 0.0f; vert[3].Tv = 1.0f;
            //vert[4].Tu = 1.0f; vert[4].Tv = 0.0f;
            //vert[5].Tu = 1.0f; vert[5].Tv = 1.0f;


            //group.vf = VertexPositionNormalTextured.Format;
            //group.vb = new VertexBuffer(typeof(VertexPositionNormalTextured), 6, device, Usage.Dynamic, group.vf, Pool.Default);
            //group.vb.SetData(vert, 0, LockFlags.None);
            group.tex = simple;
        }

        public void Render()
        {

        }

        public void Dispose()
        {
            device = null;
            if (simple != null && !simple.Disposed)
            {
                simple.Dispose();
            }
            simple = null;
        }
    }
}
