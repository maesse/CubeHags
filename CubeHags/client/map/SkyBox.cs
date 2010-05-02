using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using CubeHags.client.render;
using SlimDX;
using System.Drawing;
using CubeHags.client.render.Formats;

namespace CubeHags.client.map.Source
{
    public class SkyBox : RenderGroup
    {
        private List<KeyValuePair<ulong, RenderDelegate>> rendercalls = new List<KeyValuePair<ulong, RenderDelegate>>();

        public SkyBox(SourceMap map, string texturename, Entity light_environment)
        {
            Init(texturename);
            Color4 skyColor = new Color4(2f, 255f, 255f, 255f);

            if (light_environment != null)
            {
                // This makes the skybox blend in better with the rest of the world
                string lightval = light_environment.Values["_light"];
                string[] vals = lightval.Split(' ');
                if (vals.Length == 4)
                {
                    // Try to get sun light color
                    float r, g, b;
                    r = int.Parse(vals[0]);
                    g = int.Parse(vals[1]);
                    b = int.Parse(vals[2]);
                    float brightness = int.Parse(vals[3]);
                    skyColor = new Color4(brightness, r, g, b);
                }
            }

            // Create a tiny lightmap, and take white+ maxExponent from the map as a color
            // Grab brightness of environment light and convert to exponent offset
            tex2 = TextureManager.CreateTexture(1,1,Format.A16B16G16R16F, skyColor);
            SharedTexture2 = true;
        }

        public void Render()
        {
            // Todo: Cull
            Renderer.Instance.drawCalls.AddRange(rendercalls);
        }

        // Prepare for rendercalls
        public void SetupRender(Device device)
        {
            if (SharedTexture1 && tex != null)
                device.SetTexture(0, tex);
            if (SharedTexture2 && tex2 != null)
                device.SetTexture(1, tex2);
        }

         private void Init(string texturename) 
         {
             VertexPositionNormalTextured[] verts = new VertexPositionNormalTextured[36];

            // Front face
             verts[0] = new VertexPositionNormalTextured(-1.0f, 1.0f, 1.0f, 1.0f, 0.0f);
             verts[1] = new VertexPositionNormalTextured(-1.0f, -1.0f, 1.0f, 1.0f, 1.0f);
             verts[2] = new VertexPositionNormalTextured(1.0f, 1.0f, 1.0f, 0.0f, 0.0f);
             verts[3] = new VertexPositionNormalTextured(-1.0f, -1.0f, 1.0f, 1.0f, 1.0f);
             verts[4] = new VertexPositionNormalTextured(1.0f, -1.0f, 1.0f, 0.0f, 1.0f);
            verts[5] = new VertexPositionNormalTextured(1.0f, 1.0f, 1.0f, 0.0f, 0.0f);

            // Back face (remember this is facing *away* from the camera, so vertices should be
            //    clockwise order)
            verts[6] = new VertexPositionNormalTextured(-1.0f, 1.0f, -1.0f, 0.0f, 0.0f);
            verts[7] = new VertexPositionNormalTextured(1.0f, 1.0f, -1.0f, 1.0f, 0.0f);
            verts[8] = new VertexPositionNormalTextured(-1.0f, -1.0f, -1.0f, 0.0f, 1.0f);
            verts[9] = new VertexPositionNormalTextured(-1.0f, -1.0f, -1.0f, 0.0f, 1.0f);
            verts[10] = new VertexPositionNormalTextured(1.0f, 1.0f, -1.0f, 1.0f, 0.0f);
            verts[11] = new VertexPositionNormalTextured(1.0f, -1.0f, -1.0f, 1.0f, 1.0f);

            // Top face
            verts[12] = new VertexPositionNormalTextured(-1.0f, 1.0f, 1.0f, 1.0f, 0.0f);
            verts[13] = new VertexPositionNormalTextured(1.0f, 1.0f, -1.0f, 0.0f, 1.0f);
            verts[14] = new VertexPositionNormalTextured(-1.0f, 1.0f, -1.0f, 0.0f, 0.0f);
            verts[15] = new VertexPositionNormalTextured(-1.0f, 1.0f, 1.0f, 1.0f, 0.0f);
            verts[16] = new VertexPositionNormalTextured(1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
            verts[17] = new VertexPositionNormalTextured(1.0f, 1.0f, -1.0f, 0.0f, 1.0f);

            // Bottom face (remember this is facing *away* from the camera, so vertices should be
            //    clockwise order)
            verts[18] = new VertexPositionNormalTextured(-1.0f, -1.0f, 1.0f, 0.0f, 0.0f);
            verts[19] = new VertexPositionNormalTextured(-1.0f, -1.0f, -1.0f, 0.0f, 1.0f);
            verts[20] = new VertexPositionNormalTextured(1.0f, -1.0f, -1.0f, 1.0f, 1.0f);
            verts[21] = new VertexPositionNormalTextured(-1.0f, -1.0f, 1.0f, 0.0f, 0.0f);
            verts[22] = new VertexPositionNormalTextured(1.0f, -1.0f, -1.0f, 1.0f, 1.0f);
            verts[23] = new VertexPositionNormalTextured(1.0f, -1.0f, 1.0f, 1.0f, 0.0f);

            // Left face
            verts[24] = new VertexPositionNormalTextured(-1.0f, 1.0f, 1.0f, 0.0f, 0.0f);
            verts[25] = new VertexPositionNormalTextured(-1.0f, -1.0f, -1.0f, 1.0f, 1.0f);
            verts[26] = new VertexPositionNormalTextured(-1.0f, -1.0f, 1.0f, 0.0f, 1.0f);

            verts[27] = new VertexPositionNormalTextured(-1.0f, 1.0f, -1.0f, 1.0f, 0.0f);
            verts[28] = new VertexPositionNormalTextured(-1.0f, -1.0f, -1.0f, 1.0f, 1.0f);
            verts[29] = new VertexPositionNormalTextured(-1.0f, 1.0f, 1.0f, 0.0f, 0.0f);

            // Right face (remember this is facing *away* from the camera, so vertices should be
            //    clockwise order)
            verts[30] = new VertexPositionNormalTextured(1.0f, 1.0f, 1.0f, 1.0f, 0.0f);
            verts[31] = new VertexPositionNormalTextured(1.0f, -1.0f, 1.0f, 1.0f, 1.0f);
            verts[32] = new VertexPositionNormalTextured(1.0f, -1.0f, -1.0f, 0.0f, 1.0f);
            verts[33] = new VertexPositionNormalTextured(1.0f, 1.0f, -1.0f, 0.0f, 0.0f);
            verts[34] = new VertexPositionNormalTextured(1.0f, 1.0f, 1.0f, 1.0f, 0.0f);
            verts[35] = new VertexPositionNormalTextured(1.0f, -1.0f, -1.0f, 0.0f, 1.0f);
            Matrix translation = Matrix.Translation(0f,0f,0f);
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i].Position = Vector3.Multiply(verts[i].Position, 5000f);
                verts[i].Position = Vector3.TransformCoordinate(verts[i].Position, translation);
                verts[i].TextureCoordinate.X = (verts[i].TextureCoordinate.X + (1f / 512)) * (510f / 512f);
                verts[i].TextureCoordinate.Y = (verts[i].TextureCoordinate.Y + (1f / 512)) * (510f / 512f);
            }
            vb = new HagsVertexBuffer();
            vb.SetVB<VertexPositionNormalTextured>(verts, verts.Length * VertexPositionNormalTextured.SizeInBytes, VertexPositionNormalTextured.Format, Usage.WriteOnly);

            for (int i = 0; i < 6; i++)
            {
                List<uint> ind = new List<uint>();
                string texname = "skybox/"+ texturename;
                switch(i) 
                {
                    case 0: 
                        texname += "ft";
                        ind.AddRange(new uint[] { 0, 2, 1, 3, 5, 4 });
                        break;
                    case 1:
                        texname += "bk";
                        ind.AddRange(new uint[] {6,8,7,9,11,10 });
                        break;
                    case 2:
                        texname += "up";
                        ind.AddRange(new uint[] {12,14,13,15,17,16 });
                        break;
                    case 3:
                        texname += "dn";
                        ind.AddRange(new uint[] {18,20,19,21,23,22 });
                        break;
                    case 4:
                        texname += "lf";
                        ind.AddRange(new uint[] { 24,26,25,27,29,28});
                        break;
                    case 5:
                        texname += "rt";
                        ind.AddRange(new uint[] {30,32,31,33,35,34 });
                        break;
                }
                RenderItem item = new RenderItem(this, TextureManager.Instance.LoadMaterial(texname));
                item.DontOptimize = true;
                item.indices = ind;
                item.GenerateIndexBuffer();
                items.Add(item);
                rendercalls.Add(new KeyValuePair<ulong, RenderDelegate>(SortItem.GenerateBits(SortItem.FSLayer.GAME, SortItem.Viewport.STATIC, SortItem.VPLayer.SKYBOX, SortItem.Translucency.OPAQUE, item.material.MaterialID, 0, item.ib.IndexBufferID, item.vb.VertexBufferID), new RenderDelegate(item.Render)));
            }
        }
    }

   
}
