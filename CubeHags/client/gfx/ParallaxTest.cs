using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;

namespace CubeHags.client
{
    class ParallaxTest
    {
        Mesh mesh = null;
        Device device = null;
        Texture brick_wall = null;
        Texture bump_brick_wall = null;

        public ParallaxTest(Device device)
        {
            this.device = device;
            mesh = Mesh.FromFile(Renderer.Instance.device, "client/map/Disc.x", MeshFlags.Dynamic);
            brick_wall = TextureManager.Instance.LoadTexture("textures/wood.dds");
            bump_brick_wall = TextureManager.Instance.LoadTexture("textures/heightMap.dds");
        }

        public void Render()
        {
            RenderGroup group = new RenderGroup();
            group.tex = brick_wall;
            group.tex2 = bump_brick_wall;
            //group.vf = VertexPositionNormalTexturedNormalTanBitan.Format;
            //group.vd = new VertexDeclaration(Renderer.Instance.device, VertexPosTexNormalTanBitan.Elements);
            group.mesh = mesh;
            //RenderItem item = new RenderItem(group, brick_wall);
            //item.vb = new VertexBuffer(Renderer.Instance.device, VertexPosTexNormalTanBitan.SizeInBytes * 24,Usage.Dynamic, VertexPosTexNormalTanBitan.Format, Pool.Default);

            //VertexPosTexNormalTanBitan[] verts = new VertexPosTexNormalTanBitan[24];
            ////one
            //verts[0] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, 1, -1), new Vector2(0, 0),
            //    new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1));
            //verts[1] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, 1, -1), new Vector2(1, 0),
            //    new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1));
            //verts[2] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, 1, 1), new Vector2(1, 1),
            //    new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1));
            //verts[3] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, 1, 1), new Vector2(0, 1),
            //    new Vector3(0, 1, 0), new Vector3(1, 0, 0), new Vector3(0, 0, 1));

            ////two
            //verts[4] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, -1, -1), new Vector2(0, 0),
            //    new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            //verts[5] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, -1, 1), new Vector2(1, 0),
            //    new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            //verts[6] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, 1, 1), new Vector2(1, 1),
            //    new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0));
            //verts[7] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, 1, -1), new Vector2(0, 1),
            //    new Vector3(1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 1, 0));

            ////three
            //verts[8] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, 1, 1), new Vector2(0, 0),
            //    new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            //verts[9] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, -1, 1), new Vector2(1, 0),
            //    new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            //verts[10] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, 1, 1), new Vector2(1, 1),
            //    new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0));
            //verts[11] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, -1, 1), new Vector2(0, 1),
            //    new Vector3(0, 0, 1), new Vector3(-1, 0, 0), new Vector3(0, 1, 0));

            ////four
            //verts[12] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, -1, -1), new Vector2(0, 0),
            //    new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            //verts[13] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, -1, -1), new Vector2(1, 0),
            //    new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            //verts[14] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, 1, -1), new Vector2(1, 1),
            //    new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0));
            //verts[15] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, 1, -1), new Vector2(0, 1),
            //    new Vector3(0, 0, -1), new Vector3(1, 0, 0), new Vector3(0, 1, 0));



            ////five
            //verts[16] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, -1, 1), new Vector2(0, 0),
            //    new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            //verts[17] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, -1, -1), new Vector2(1, 0),
            //    new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            //verts[18] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, 1, -1), new Vector2(1, 1),
            //    new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0));
            //verts[19] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, 1, 1), new Vector2(0, 1),
            //    new Vector3(-1, 0, 0), new Vector3(0, 0, -1), new Vector3(0, 1, 0));



            ////six
            //verts[20] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, -1, -1), new Vector2(0, 0),
            //    new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1));
            //verts[21] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, -1, -1), new Vector2(1, 0),
            //    new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1));
            //verts[22] = new VertexPosTexNormalTanBitan(
            //    new Vector3(-1, -1, 1), new Vector2(1, 1),
            //    new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1));
            //verts[23] = new VertexPosTexNormalTanBitan(
            //    new Vector3(1, -1, 1), new Vector2(0, 1),
            //    new Vector3(0, -1, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1));

            //for (int i = 0; i < 24; i++)
            //{
            //    verts[i].Position.Scale(20f);
            //}

            //item.vb.SetData(verts, 0, LockFlags.None);
            //item.nVerts = 24;
            //group.items.Add(0, item);


        }
    }
}
