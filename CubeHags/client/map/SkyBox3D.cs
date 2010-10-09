using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.client.render;

namespace CubeHags.client.map.Source
{
    public class SkyBox3D : RenderGroup
    {
        SourceMap map;

        // Positioning
        Entity sky_camera;
        public Vector3 startPosition = Vector3.Zero;
        public float Scale = 16f;

        // Rendering
        int lastCluster = -1;
        int VisCount = 1;
        public Dictionary<int, List<RenderItem>> visibleRenderItems = new Dictionary<int, List<RenderItem>>();

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
                position.X = float.Parse(origin_values[0], System.Globalization.CultureInfo.InvariantCulture);
                position.Y = float.Parse(origin_values[1], System.Globalization.CultureInfo.InvariantCulture);
                position.Z = float.Parse(origin_values[2], System.Globalization.CultureInfo.InvariantCulture);
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
            if (cluster == -1)
            {
                lastCluster = -1;
                //// Clear last runs visible renderitems
                //foreach (List<RenderItem> itlist in visibleRenderItems.Values)
                //{
                //    itlist.Clear();
                //}
            } else if (cluster != lastCluster)
            {
                lastCluster = cluster;
                // Clear last runs visible renderitems
                foreach (List<RenderItem> itlist in visibleRenderItems.Values)
                {
                    itlist.Clear();
                }
                

                // Generate render calls
                map.MarkVisible(cluster, VisCount);
                RecursiveWorldNode(0);
            }
        }

        private void RecursiveWorldNode(int nodeid)
        {
            do
            {
                if (nodeid >= 0)
                {
                    // node
                    dnode_t node = map.world.nodes[nodeid];

                    if (node.lastVisibleCount != VisCount)
                        return;

                    RecursiveWorldNode(node.children[0]);
                    nodeid = node.children[1];
                }
                else
                {
                    // leaf
                    dleaf_t leaf = map.world.leafs[-(nodeid + 1)];

                    // Check for displacements
                    leaf.lastVisibleCount = VisCount;
                    if (leaf.DisplacementIndexes != null)
                    {
                        KeyValuePair<int,int>[] indx = leaf.DisplacementIndexes;
                        for (int i = 0; i < indx.Length; i++)
                        {
                            Face face2 = map.world.faces[indx[i].Key];
                            if (face2.lastVisCount != VisCount)
                            {
                                face2.lastVisCount = VisCount;
                                visibleRenderItems[face2.item.material.MaterialID].Add(face2.item);
                            }
                        }
                    }


                    // Iterate over contained faces - this will loop a lot!
                    for (int j = 0; j < leaf.numleaffaces; j++)
                    {
                        int index = j + leaf.firstleafface;

                        Face face = map.world.faces[map.world.leafFaces[index]];
                        // is face already processed this frame?
                        if (face != null && face.lastVisCount != VisCount)
                        {
                            face.lastVisCount = VisCount;
                            if (face.item != null)
                                visibleRenderItems[face.item.material.MaterialID].Add(face.item); // possible 10%+ optimization here
                        }
                    }
                    //// Handle static props
                    //for (int i = 0; i < leaf.staticProps.Count; i++)
                    //{
                    //    if (leaf.staticProps[i].prop_t.lastVisibleCount != VisCount)
                    //    {
                    //        leaf.staticProps[i].prop_t.lastVisibleCount = VisCount;
                    //        leaf.staticProps[i].prop_t.lastVisibleLeaf = (ushort)-(nodeid + 1);
                    //        // middle of leaf
                    //        //Vector3 pos = (leaf.maxs - leaf.mins) / 2;
                    //        //leaf.staticProps[i].prop_t.Origin

                    //        visibleProps.Add(leaf.staticProps[i]);
                    //    }
                    //    else
                    //        leaf.staticProps[i].prop_t.lastVisibleLeaf = (ushort)-(nodeid + 1);
                    //}
                    break;
                }
            } while (true);
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
