using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
using Ionic.Zip;
using CubeHags.client.gfx;
using System.Drawing;
using System.Collections;
using CubeHags.client.render.Formats;
using CubeHags.client.render;
using CubeHags.client.common;
using CubeHags.client.input;
using System.Windows.Forms;
using CubeHags.common;

namespace CubeHags.client.map.Source
{
    public class SourceMap : RenderGroup
    {
        // Imported structure
        public SkyBox skybox;
        public SkyBox3D skybox3d;
        public World world;

        // Rendering
        public bool UseBSP = true;
        public bool LockPVS = false;

        public dleaf_t CurrentLeaf;
        public int CurrentLeafID;
        public int CurrentCluster;
        public int LastCluster = -2;
        public int VisCount = 0;
        private Vector3 lastPosition = Vector3.Zero;
        private Vector3 lastLook = Vector3.Zero;
        private Vector3 lastUp = Vector3.Zero;
        
        // Buffers
        Dictionary<int, List<RenderItem>> visibleRenderItems = new Dictionary<int, List<RenderItem>>();
        List<KeyValuePair<ulong, RenderDelegate>> renderCalls = new List<KeyValuePair<ulong, RenderDelegate>>();
        HagsIndexBuffer batchIB = new HagsIndexBuffer();
        uint[] dynamicIndices = new uint[10000];
        HagsVertexBuffer propVertexBuffer = new HagsVertexBuffer();
        int propVertexBufferOffset = 0;
        List<SourceProp> visibleProps = new List<SourceProp>();

        HagsVertexBuffer bspVB = new HagsVertexBuffer();

        public List<VertexPositionColor> bboxVerts = new List<VertexPositionColor>();

        public SourceMap(World world)
        {
            this.world = world;
            //redCube.Color = new Color4[6];
            //for (int i = 0; i < 6; i++)
            //{
            //    redCube.Color[i] = new Color4(1.0f, 0.0f, 0.0f);
            //}
            //ambientLightTexture = new Texture(Renderer.Instance.device, 256, 256, 0, Usage.Dynamic | Usage.WriteOnly, Format.A16B16G16R16F, Pool.Default);
        }

        public new void Init()
        {
            this.SharedTexture2 = true;
            this.tex2 = world.LightmapTexture;
            this.stride = VertexPositionNormalTexturedLightmap.SizeInBytes;
            
            // Make renderitem for each face
            for (int fi = 0; fi < world.faces.Length;fi++ )
            {
                Face face = world.faces[fi];
                face.Format = VertexPositionNormalTexturedLightmap.Format;
                RenderItem item = new RenderItem(this, face.texinfo.texdata_t.mat);

                //if (face.texinfo.texdata_t.mat != null && face.texinfo.texdata_t.mat.Bumpmap)
                //    face.Format = VertexPositionNormalTexturedLightmapTangent.Format;

                // Create index list for non-displacement faces only
                if (face.face_t.dispinfo == -1 && !face.HasDisplacement && face.VertexOffset != -1)
                {
                    // Make TriangleList
                    int newIndices = (face.face_t.numedges - 2) * 3;
                    face.indices = new uint[newIndices];
                    face.nVerts = HagsIndexBuffer.GetVertexCount(item.indices);
                    for (int i = 0; i < (face.face_t.numedges - 2); i++)
                    {
                        face.indices[3 * i] = (uint)(face.VertexOffset);
                        face.indices[3 * i + 1] = (uint)(face.VertexOffset + i + 1);
                        face.indices[3 * i + 2] = (uint)(face.VertexOffset + i + 2);
                    }
                    item.indices = new List<uint>(face.indices);
                }
                else
                {
                    // Use pre-generated displacement index list
                    if (face.indices != null)
                    {
                        item.indices = new List<uint>(face.indices);
                        //face.nVerts = HagsIndexBuffer.GetVertexCount(item.indices);
                    }
                }

                // Setup item
                item.DontOptimize = true;
                item.face = face;
                item.Type = PrimitiveType.TriangleList;
                item.nVerts = face.nVerts;
                item.Init();
                world.faces[fi].item = item;
                items.Add(item);
            }
            
            // Create shared vertex buffer
            int vertexBytes = world.verts.Count * VertexPositionNormalTexturedLightmap.SizeInBytes;
            vb = new HagsVertexBuffer();
            vb.SetVB<VertexPositionNormalTexturedLightmap>(world.verts.ToArray(), vertexBytes, VertexPositionNormalTexturedLightmap.Format, Usage.WriteOnly);
            ib = new HagsIndexBuffer();

            Entity light_environment = null;
            foreach (Entity ent in world.Entities)
            {
                //System.Console.WriteLine("\n"+ ent.ClassName);
                foreach (string val in ent.Values.Keys)
                {
                    //System.Console.WriteLine("\t"+val + ": " + ent.Values[val]);
                }
                if (ent.ClassName == "light_environment")
                {
                    light_environment = ent;
                }
                else if (ent.ClassName.Equals("sky_camera"))
                {
                    skybox3d = new SkyBox3D(this, ent);
                }
            }

            if (skybox3d == null)
            {
                // Look for area 1
            }

            // Handle worldspawn entity (skybox)
            if (world.Entities[0].ClassName == "worldspawn")
            {
                if (world.Entities[0].Values.ContainsKey("skyname"))
                {
                    string skyname = world.Entities[0].Values["skyname"];
                    skybox = new SkyBox(this, skyname, light_environment);
                }
            }

            

            // Make leafs point towards nodes also
            SetParent(ref world.nodes[0], ref world.nodes[0]);
            world.nodes[0].parent = null;

            // Prepare visibleRenderItems memorisation structure
            foreach (RenderItem item in items)
            {
                if (!visibleRenderItems.ContainsKey(item.material.MaterialID))
                {
                    visibleRenderItems.Add(item.material.MaterialID, new List<RenderItem>());
                    if (skybox3d != null)
                        skybox3d.visibleRenderItems.Add(item.material.MaterialID, new List<RenderItem>());
                }
                
            }
            
                
        }

        public void VisualizeBBox()
        {
            if (bboxVerts.Count == 0)
                return;
            bspVB.SetVB<VertexPositionColor>(bboxVerts.ToArray(), bboxVerts.Count * VertexPositionColor.SizeInBytes, VertexPositionColor.Format, Usage.WriteOnly);
            int nVerts = bboxVerts.Count;
            bspVB.SetVD(new VertexDeclaration(Renderer.Instance.device, VertexPositionColor.Elements));
            RenderDelegate dlg = new RenderDelegate((effect, device, setMaterial) =>
            {
                device.DrawPrimitives(PrimitiveType.TriangleList, 0, nVerts / 3);
            });
            ulong id = SortItem.GenerateBits(SortItem.FSLayer.EFFECT, SortItem.Viewport.STATIC, SortItem.VPLayer.EFFECT, SortItem.Translucency.NORMAL, 0, 0, 0, bspVB.VertexBufferID);
            Renderer.Instance.drawCalls.Add(new KeyValuePair<ulong, RenderDelegate>(id, dlg));
        }

        void VisualizeBSP()
        {
            List<VertexPositionColor> verts = new List<VertexPositionColor>();
            float red = 1.0f;
            float green = 0.0f;

            

            int i = 0;
            foreach (dleaf_t nod in world.leafs)
            {
                if (i == 4 || i == 5)
                {
                    verts.AddRange(MiscRender.CreateBox(nod.mins + new Vector3(1f, 1f, 1f), nod.maxs - new Vector3(1f, 1f, 1f), new Color4(0.3f, 0.3f, 0.1f, 1.0f)));
                }
                else
                {
                    verts.AddRange(MiscRender.CreateBox(nod.mins + new Vector3(1f, 1f, 1f), nod.maxs - new Vector3(1f, 1f, 1f), new Color4(0.1f, red, green, 0.0f)));
                }
                
                
                if (red > 0.0f && green < 1.0f)
                {
                    green += 0.1f;
                }
                else if (green > 0.9f)
                {
                    red -= 0.1f;
                }
                i++;
            }
            


            bspVB.SetVB<VertexPositionColor>(verts.ToArray(), verts.Count * VertexPositionColor.SizeInBytes, VertexPositionColor.Format, Usage.WriteOnly);
            bspVB.SetVD(new VertexDeclaration(Renderer.Instance.device, VertexPositionColor.Elements));
            RenderDelegate dlg = new RenderDelegate((effect, device, setMaterial) =>
            {
                device.DrawPrimitives(PrimitiveType.TriangleList, 0, verts.Count/3);
            });
            ulong id = SortItem.GenerateBits(SortItem.FSLayer.EFFECT, SortItem.Viewport.STATIC, SortItem.VPLayer.EFFECT, SortItem.Translucency.NORMAL, 0, 0, 0, bspVB.VertexBufferID);
            Renderer.Instance.drawCalls.Add(new KeyValuePair<ulong, RenderDelegate>(id, dlg));
        }

        public void Render(Device device)
        {

            
            // Get current leaf & cluster
            if (!LockPVS)
            {
                VisCount++;
                CurrentLeafID = FindLeaf(Renderer.Instance.Camera.position);
                CurrentLeaf = world.leafs[CurrentLeafID];
                CurrentCluster = CurrentLeaf.cluster;
                //if (Renderer.Instance.window != null)
                //    Renderer.Instance.window.CurrentCluster.Text = CurrentCluster.ToString();
            }
            if (skybox != null)
                skybox.Render();

            if (skybox3d != null)
                skybox3d.Render();


            bboxVerts.Clear();
            
            Camera cam = Renderer.Instance.Camera;
            // Update visibility if new position
            if (CurrentCluster != LastCluster || !cam.position.Equals(lastPosition))
            {
                // save position
                lastPosition = cam.position;
                
                // Clear last runs visible renderitems
                foreach (List<RenderItem> itlist in visibleRenderItems.Values)
                {
                    itlist.Clear();
                }
                visibleProps.Clear();
                
                // Mark visible leafs and nodes
                MarkVisible(CurrentCluster, VisCount);
                // Move though BSP tree
                RecursiveWorldNode(0);

                // Always show displacements
                //foreach (RenderItem item in dispRenderItems)
                //    visibleRenderItems[item.material.MaterialID].Add(item);

                List<DynamicItem> dynItems = new List<DynamicItem>();
                List<KeyValuePair<ulong, RenderDelegate>> calls = new List<KeyValuePair<ulong, RenderDelegate>>();

                ushort dist = 0;
                SortItem.Translucency trans = SortItem.Translucency.OPAQUE;
                DynamicItem dynItem = new DynamicItem();
                uint min = 9999999, max = 0;
                int dynoffset = 0;
                // Group items that needs to be batched
                Dictionary<int, List<RenderItem>> renderItems = visibleRenderItems;
                for (int itemPass = 0; itemPass < 2; itemPass++)
                {
                    if (itemPass == 1) {
                        if(skybox3d != null)
                            renderItems = skybox3d.visibleRenderItems;
                        else
                            break;
                    }

                        foreach (List<RenderItem> grouplist in renderItems.Values)
                        {
                            // Create new batch
                            dynItem = new DynamicItem();
                            dynItem.vb = this.vb;
                            dynItem.ib = batchIB;
                            dynItem.IndiceStartIndex = dynoffset;
                            trans = SortItem.Translucency.OPAQUE;
                            dist = 0;
                            min = 9999999;
                            max = 0;

                            // Add items to batch
                            foreach (RenderItem item in grouplist)
                            {
                                if (item.face.HasDisplacement)
                                {
                                    int test = 2;
                                }
                                if (item.indices.Count == 0 || item.nVerts == 0)
                                    continue;

                                // Not too sure about how alpha batching should be handled.. Maybe sort before batching?
                                if (item.material.Alpha)
                                {
                                    trans = SortItem.Translucency.NORMAL;
                                    dist = (ushort)CurrentDistFromPlane(item.face.plane_t);
                                }

                                dynItem.material = item.material;
                                dynItem.Type = item.Type;

                                if (item.IndiceMin < min)
                                    min = item.IndiceMin;
                                if (item.IndiceMax > max)
                                    max = item.IndiceMax;

                                // Append to current batch
                                dynItem.nIndices += item.indices.Count;
                                dynItem.nVerts += item.nVerts;



                                // Ensure dynamic array is big enough
                                if (dynoffset + item.indices.Count > dynamicIndices.Length)
                                {
                                    uint[] newlist = new uint[dynamicIndices.Length * 2];
                                    dynamicIndices.CopyTo(newlist, 0);
                                    dynamicIndices = newlist;
                                }
                                // Copy indices into the indice buffer
                                item.indices.CopyTo(0, dynamicIndices, dynoffset, item.indices.Count);
                                dynoffset += item.indices.Count;
                            }

                            // Add drawcall for batch
                            if (dynItem.nIndices > 0)
                            {
                                // Important for ATI cards
                                dynItem.nVerts = (int)max - (int)min + 1;
                                dynItem.lowestIndiceValue = (int)min;
                                // Complete current batch
                                ulong k = SortItem.GenerateBits(SortItem.FSLayer.GAME, SortItem.Viewport.DYNAMIC, itemPass==0?SortItem.VPLayer.WORLD:SortItem.VPLayer.SKYBOX3D, trans, dynItem.material.MaterialID, dist, batchIB.IndexBufferID, vb.VertexBufferID);
                                RenderDelegate v = new RenderDelegate(dynItem.Render);
                                calls.Add(new KeyValuePair<ulong, RenderDelegate>(k, v));
                            }
                        }
                }
                // Set IB data for batches
                batchIB.SetIB<uint>(dynamicIndices, dynoffset * sizeof(uint), Usage.WriteOnly | Usage.Dynamic, false, Pool.Default, dynoffset);
                
                Dictionary<string, List<SourceProp>> propList = new Dictionary<string, List<SourceProp>>();
                // Group up props for instancing
                foreach (SourceProp prop in visibleProps)
                {
                    if (propList.ContainsKey(prop.prop_t.PropName))
                        propList[prop.prop_t.PropName].Add(prop);
                    else
                        propList.Add(prop.prop_t.PropName, new List<SourceProp>(new SourceProp[] { prop }));
                }

                int propVBOffset = 0;
                
                int totalBytes = 0;
                int lightcubeid = 0;
                
                List<VertexPropInstance> verts = new List<VertexPropInstance>();
                // Create InstanceItems
                foreach (List<SourceProp> props in propList.Values)
                {
                    Dictionary<ushort, KeyValuePair<int, CompressedLightCube>> lightcubes = new Dictionary<ushort, KeyValuePair<int, CompressedLightCube>>();
                    //if (props.Count == 0 || !props[0].prop_t.PropName.Contains("models/props/cs_office/Vending_machine.mdl") ||!props[0].prop_t.PropName.Contains("Light"))
                    //    continue;
                    if (props[0].srcModel == null)
                        continue;
                    InstanceItem instItem = new InstanceItem();

                    // Add mesh
                    instItem.Parts = props[0].srcModel.BodyParts;
                    instItem.InstanceBufferOffset = propVertexBufferOffset + (verts.Count * VertexPropInstance.SizeInBytes);
                    int propInstanceCount = 0;
                    // Generate Instance vert for every prop in this group
                    foreach (SourceProp prop in props)
                    {
                        
                        ushort propLeaf = prop.prop_t.lastVisibleLeaf;
                        //int lightid = 0;
                        //// Grap lightcube id if already added
                        //if (lightcubes.ContainsKey(propLeaf) )
                        //{
                        //    lightid = lightcubes[propLeaf].Key;
                        //}
                        //// else add it now
                        //else if (true) // 
                        //{
                        //    lightid = lightcubeid++;
                        //    //leafs[propLeaf].ambientLighting.Color[0].
                        //    //Matrix matrix = new Matrix();
                        //    //matrix.set_Rows(0, leafs[propLeaf].ambientLighting.Color[0].ToVector4());
                        //    //matrix.set_Rows(1, leafs[propLeaf].ambientLighting.Color[1].ToVector4());
                        //    //matrix.set_Rows(2, leafs[propLeaf].ambientLighting.Color[2].ToVector4());
                        //    //instItem.lightcubearr.Add(matrix);
                        //    //matrix = new Matrix();
                        //    //matrix.set_Rows(0, leafs[propLeaf].ambientLighting.Color[3].ToVector4());
                        //    //matrix.set_Rows(1, leafs[propLeaf].ambientLighting.Color[4].ToVector4());
                        //    //matrix.set_Rows(2, leafs[propLeaf].ambientLighting.Color[5].ToVector4());
                        //    //instItem.lightcubearr.Add(matrix);

                        //    //for (int j = 0; j < 6; j++)
                        //    //{
                        //    //    Vector3 val = leafs[propLeaf].ambientLighting.Color[j].ToVector3();
                        //    //    float redval = 1f;
                        //    //    if (lightid % 2 == 0)
                        //    //        redval = 5f;
                        //    //    instItem.lightcubearr.AddRange(new float[] { redval, 0f, 0f });
                        //    //    //for (int veci = 0; veci < 3; veci++)
                        //    //    //{
                        //    //    //    instItem.lightcubearr.Add(val[veci]);
                                    
                        //    //    //}
                        //    //    //lightcubearr.Add(redCube.Color[j].ToVector4());

                        //    //}
                        //    lightcubes.Add(propLeaf, new KeyValuePair<int, CompressedLightCube>(lightid, leafs[propLeaf].ambientLighting));
                        //}
                        //else
                        //{
                        //    lightid = 63;
                        //    for (int i = 0; i < 6; i++)
                        //    {
                        //        Vector3 val = redCube.Color[i].ToVector3();
                        //        for (int veci = 0; veci < 3; veci++)
                        //        {
                        //            instItem.lightcubearr.Add(val[veci]);
                        //        }
                                
                        //    }
                        //}
                        float pitch, roll, yaw;
                        pitch = prop.prop_t.Angles.Z * (float)(Math.PI / 180f);
                        roll = prop.prop_t.Angles.Y * (float)(Math.PI / 180f);
                        yaw = prop.prop_t.Angles.X * (float)(Math.PI / 180f);
                        // Order of multiplication very important
                        
                        Matrix modelMatrix = Matrix.RotationYawPitchRoll(yaw, pitch, roll) * Matrix.Translation(prop.prop_t.Origin);
                        VertexPropInstance instvert = new VertexPropInstance(modelMatrix, world.leafs[propLeaf].ambientLighting);
                        verts.Add(instvert);
                        instItem.nInstances++;
                        propInstanceCount++;
                    }

                    // Create instance vertexbuffer
                    instItem.vb = propVertexBuffer;
                    
                    totalBytes += propInstanceCount * VertexPropInstance.SizeInBytes;
                    // Create rendercall to this instanceitem
                    ulong callkey = SortItem.GenerateBits(SortItem.FSLayer.GAME, SortItem.Viewport.INSTANCED, SortItem.VPLayer.WORLD, SortItem.Translucency.OPAQUE, 0, 0, 0, 0);
                    RenderDelegate callvalue = new RenderDelegate(instItem.Render);
                    calls.Add(new KeyValuePair<ulong, RenderDelegate>(callkey, callvalue));
                }
                propVertexBuffer.SetVB<VertexPropInstance>(verts.ToArray(), totalBytes, VertexPropInstance.Format, Usage.Dynamic | Usage.WriteOnly, propVertexBufferOffset);
                if (propVertexBufferOffset > 0)
                    propVertexBufferOffset = 0;
                else
                    propVertexBufferOffset = totalBytes;

                //List<Vector4> lightcubearr = new List<Vector4>();
                //foreach (KeyValuePair<int, CompressedLightCube> kv in lightcubes.Values)
                //{
                //    // Write 6 * 4 * floats
                //    CompressedLightCube cube = kv.Value;
                //    for (int j = 0; j < 6; j++)
                //    {
                //        lightcubearr.Add(cube.Color[j].ToVector4());
                //        //lightcubearr.Add(redCube.Color[j].ToVector4());

                //    }
                //    if (lightcubearr.Count == 31 * 6)
                //        break;
                //    //ds.Seek((long)(y * 1024L * 8L) + (long)(x * 8L), System.IO.SeekOrigin.Begin);
                //   // for (int j = 0; j < 6; j++)
                //   // {
                //   //     Half[] half = Half.ConvertToHalf(new float[] { cube.Color[j].Red, cube.Color[j].Green, cube.Color[j].Blue });
                //   //     ds.Write<Half4>(new Half4(half[0], half[1], half[2], new Half()));
                //   // }
                //}
               // ambientLightTexture.UnlockRectangle(0);
                
                

                //if (lightcubearr.Count <= 32*6 && lightcubearr.Count > 0)
                //    Renderer.Instance.effect.SetValue<Vector4>("ambientLight", lightcubearr.ToArray());
                //RenderItem thatone = null;
                //foreach (SourceModel chl in prop.items)
                //{
                //    foreach (BodyPart item in chl.BodyParts)
                //    {
                //        foreach (Model mdl in item.Models)
                //        {
                //            if (mdl.Meshes[0].items[0].vb != null)
                //            {
                //                thatone = mdl.Meshes[0].items[0];
                //                break;
                //            }
                //        }

                //    }

                //}
                //if (thatone != null)
                //{
                //    ushort ibid;
                //    if (thatone.ib == null)
                //        ibid = 0;
                //    else
                //        ibid = thatone.ib.IndexBufferID;
                //    ulong callkey = SortItem.GenerateBits(SortItem.FSLayer.GAME, SortItem.Viewport.STATIC, SortItem.VPLayer.WORLD, SortItem.Translucency.NORMAL, thatone.material.MaterialID, 0, ibid, thatone.vb.VertexBufferID);
                //    RenderDelegate callvalue = new RenderDelegate(thatone.Render);
                //    calls.Add(new KeyValuePair<ulong, RenderDelegate>(callkey, callvalue));
                //}

                // submit call to renderer
                renderCalls = calls;
                Renderer.Instance.drawCalls.AddRange(renderCalls);
            }
            else
            {
                Renderer.Instance.drawCalls.AddRange(renderCalls);
            }

            LastCluster = CurrentCluster;

            //VisualizeBSP();
        }

        

        // current cluster, cluster to test against
        private bool[] GetPVS(int visCluster)
        {
            int i = visCluster;// (visCluster >> 3);
            if (visCluster < 0 || world.visibility == null)
            {
                return null;
            }

            if (world.vis.byteofs == null)
            {
                bool[] outp = new bool[world.numClusters+1];
                for (int j = 0; j < outp.Length; j++)
                {
                    outp[j] = true;
                }
                return outp;
            }

            int v = world.vis.byteofs[i][0]; // offset into byte-vector
            bool[] output = new bool[world.numClusters];
            for (int c = 0; c < world.numClusters; v++)
            {
                if (world.visibility[v] == (byte)0)
                {
                    // Skip
                    c += 8 * world.visibility[++v];
                }
                else
                {
                    // Add visible info
                    for (byte bit = 1; bit != 0; bit *= 2, c++)
                    {
                        if ((world.visibility[v] & bit) != 0)
                        {
                            output[c] = true;
                        }
                    }
                }
            }

            return output;
        }

        public void SetupVertexBuffer(Device device)
        {
            if (SharedTexture1 && tex != null)
                device.SetTexture(0, tex);
            if (SharedTexture2 && world != null && world.LightmapTexture != null)
                device.SetTexture(1, world.LightmapTexture);
        }

        // Returns list of RenderItems that are visible from cluster
        public void MarkVisible(int cluster, int VisCount)
        {
            dnode_t parent;
            // Return everything if not inside a cluster
            if (cluster == -1)
            {
                for (int i = 0; i < world.leafs.Length; i++)
                {
                    dleaf_t leaf = world.leafs[i];

                    leaf.lastVisibleCount = VisCount;
                    if (leaf.parent != null)
                    {
                        parent = leaf.parent;
                        do
                        {
                            if (parent.lastVisibleCount == VisCount)
                                break;
                            SetParentRef(ref parent, ref parent.parent);
                        } while (parent != null);
                    }
                }
                return;
            }
            bool[] clusterdata = GetPVS(cluster);
            if (clusterdata == null)
                return;

            for (int i = 0; i < world.leafs.Length; ++i)
            {


                if (world.leafs[i].cluster < 0 || (world.leafs[i].cluster >= world.numClusters && world.numClusters != 0))
                    continue;

                dleaf_t leaf = world.leafs[i];

                // Check pvs
                if(!clusterdata[leaf.cluster])
                    continue;

                if (leaf.lastVisibleCount == VisCount)
                    continue;
                
                leaf.lastVisibleCount = VisCount;
                if (leaf.parent == null)
                    continue;

                parent = leaf.parent;
                do
                {
                    if (parent.lastVisibleCount == VisCount)
                        break;
                    SetParentRef(ref parent, ref parent.parent);
                } while (parent != null);
                
            }
            
            //return list;
        }

        private void SetParentRef(ref dnode_t parent, ref object newparent)
        {

            parent.lastVisibleCount = VisCount;
            parent = (dnode_t)newparent;
        }

        private void RecursiveWorldNode(int nodeid)
        {
            do
            {
                if (nodeid >= 0)
                {
                    // node
                    dnode_t node = world.nodes[nodeid];

                    if (node.lastVisibleCount != VisCount)
                        return;

                    RecursiveWorldNode(node.children[0]);
                    nodeid = node.children[1];
                }
                else
                {
                    // leaf
                    dleaf_t leaf = world.leafs[-(nodeid + 1)];
                    
                    // Check for displacements
                    leaf.lastVisibleCount = VisCount;
                    if (leaf.DisplacementIndexes != null)
                    {
                        int[] indx = leaf.DisplacementIndexes;
                        for (int i = 0; i < indx.Length; i++)
                        {
                            Face face2 = world.faces[indx[i]];
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
                        
                        Face face = world.faces[world.leafFaces[index]];
                        // is face already processed this frame?
                        if (face != null && face.lastVisCount != VisCount)
                        {
                            face.lastVisCount = VisCount;
                            if(face.item != null)
                                visibleRenderItems[face.item.material.MaterialID].Add(face.item); // possible 10%+ optimization here
                        }
                    }
                    // Handle static props
                    for (int i = 0; i < leaf.staticProps.Count; i++)
                    {
                        if (leaf.staticProps[i].prop_t.lastVisibleCount != VisCount)
                        {
                            leaf.staticProps[i].prop_t.lastVisibleCount = VisCount;
                            leaf.staticProps[i].prop_t.lastVisibleLeaf = (ushort)-(nodeid + 1);
                            // middle of leaf
                            //Vector3 pos = (leaf.maxs - leaf.mins) / 2;
                            //leaf.staticProps[i].prop_t.Origin

                            visibleProps.Add(leaf.staticProps[i]);
                        }else
                            leaf.staticProps[i].prop_t.lastVisibleLeaf = (ushort)-(nodeid + 1);
                    }
                    break;
                }
            } while (true);
        }

        // Sets parents for nodes and leafs, allowing a backwards trace
        private void SetParent(ref dnode_t node, ref dnode_t parent)
        {
            node.parent = parent;
            if (node.children[0] >= 0)
            {
                SetParent(ref world.nodes[node.children[0]], ref node);
            }
            else
            {
                world.leafs[-(node.children[0] + 1)].parent = node;   
            }

            if (node.children[1] >= 0)
                SetParent(ref world.nodes[node.children[1]], ref node);
            else
                world.leafs[-(node.children[1] + 1)].parent = node;
        }

        public static double CurrentDistFromPlane(cplane_t plane)
        {
            return Math.Abs(Vector3.Dot(Renderer.Instance.Camera.position, plane.normal) - plane.dist);
        }

        // Find leaf containing this camera position
        private int FindLeaf(Vector3 camPos)
        {
            int index = 0;
            while (index >= 0)
            {
                dnode_t node = world.nodes[index];
                cplane_t plane = world.planes[node.planenum];

                double distance = Vector3.Dot(camPos, plane.normal) - plane.dist;

                if (distance > 0)
                    index = node.children[0];
                else
                    index = node.children[1];
            }

            return -index - 1;
        }

        public int GetClusterFromPosition(Vector3 position)
        {
            return world.leafs[FindLeaf(position)].cluster;
        }

        public new void Dispose()
        {
            base.Dispose(); // Dispose rendergroup and children
            if (world != null && world.LightmapTexture != null) world.LightmapTexture.Dispose();
            if(world != null && world.Pakfile != null) world.Pakfile.Dispose();
            if (batchIB != null) batchIB.Dispose();
            if (skybox != null)
                skybox.Dispose();
        }

    }
}
