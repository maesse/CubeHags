using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX;
using CubeHags.client.render;

namespace CubeHags.client.map.Source
{
    public class SkyBox3D : RenderGroup
    {
        Entity sky_camera;
        SourceMap map;

        public Vector3 startPosition = Vector3.Zero;
        public float Scale = 16f;

        public SkyBox3D(SourceMap map, Entity sky_camera)
        {
            this.sky_camera = sky_camera;
            this.map = map;

            // Get origin of 3d skybox
            string origin = sky_camera.Values["origin"];
            origin = origin.Replace('.', ','); // Needed for proper C# float parsing
            string[] origin_values = origin.Split(' ', '\t');
            if (origin_values.Length == 3)
            {
                Vector3 position = Vector3.Zero;
                position.X = float.Parse(origin_values[0]);
                position.Y = float.Parse(origin_values[1]);
                position.Z = float.Parse(origin_values[2]);
                startPosition = SourceParser.SwapZY(position);
            }
            
            // Get scale of 3d skybox
            if (sky_camera.Values.ContainsKey("scale"))
            {
                float result;
                if (float.TryParse(sky_camera.Values["scale"], out result))
                {
                    Scale = result;
                }
            }
            
        }

        public void Render()
        {
            int cluster = map.GetClusterFromPosition(GetSkyboxPosition(Renderer.Instance.Camera.position));
            //if (cluster != -1)
            {
                // Generate render calls
                //List<RenderItem> list = map.MarkVisible(cluster);
                ////List<KeyValuePair<ulong, RenderDelegate>> calls = new List<KeyValuePair<ulong, RenderDelegate>>();
                //foreach (RenderItem item in list)
                //{
                //    ushort ibid;
                //    if(item.ib == null)
                //        ibid = 0;
                //    else
                //        ibid = item.ib.IndexBufferID;
                //    ulong callkey = SortItem.GenerateBits(SortItem.FSLayer.GAME, SortItem.Viewport.STATIC, SortItem.VPLayer.SKYBOX3D, SortItem.Translucency.OPAQUE, item.material.MaterialID, 0,ibid , item.vb.VertexBufferID);
                //    RenderDelegate callvalue = new RenderDelegate(item.Render);
                //    // submit call to renderer
                //    Renderer.Instance.drawCalls.Add(new KeyValuePair<ulong, RenderDelegate>(callkey, callvalue));
                //}
                
            }
        }

        public Vector3 GetSkyboxPosition(Vector3 cameraPosition) 
        {
            // Scale camera position with skybox scale-factor
            Vector3 origin_relative = Vector3.Multiply(cameraPosition, 1f/Scale);

            // Add to skybox origin
            return Vector3.Add(origin_relative, startPosition);
        }
    }
}
