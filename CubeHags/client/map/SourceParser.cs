using System;
using System.Collections.Generic;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
using System.IO;
using Ionic.Zip;
using System.Drawing;
using CubeHags.client.gfx;
using CubeHags.client.common;
using CubeHags.common;
using CubeHags.client.render.Formats;

namespace CubeHags.client.map.Source
{
    public class World
    {
        public string Name; // ie: maps/tim_dm2.bsp
        public string BaseName; // ie: tim_dm2

        public List<int> leafbrushes = new List<int>();
        public List<cplane_t> planes = new List<cplane_t>();

        public int numNodes;
        public dnode_t[] nodes;

        public int numLeafs;
        public dleaf_t[] leafs;

        public Color3[] LightData;
        public CompressedLightCube[] LightGrid;
        public ddispinfo_t[] ddispinfos;
        public byte[] dispLightmapSamples;
        public dDispVert[] dispVerts;

        public List<VertexPositionNormalTexturedLightmap> verts = new List<VertexPositionNormalTexturedLightmap>();
        public Face[] faces;
        public face_t[] faces_t;
        public texinfo_t[] texinfos;
        public Texture LightmapTexture;
        public ZipFile Pakfile;
        public List<Entity> Entities;
        public cmodel_t[] cmodels;

        public StaticPropLeafLump_t propleafs;
        public StaticPropLump_t[] props;
        public List<SourceProp> sourceProps = new List<SourceProp>();

        public List<int> leafFaces = new List<int>();
        public List<dbrushside_t> brushsides = new List<dbrushside_t>();
        public List<dbrush_t> brushes = new List<dbrush_t>();

        public int numClusters;
        public int clusterBytes;
        public byte[] visibility;
        public dvis_t vis;
        public bool vised;			// if false, visibility is just a single cluster of ffs

        public string EntityString;
        public int EntityParsePoint;        
    }
    public class SourceParser
    {
        static Size LightmapSize = new Size(1024, 1024);
        static float[]	power2_n = new float[256];
        static double[] g_aPowsOfTwo = new double[257];
        static int nVertIndex = 0;
        public static int ALLOWEDVERTS = 289;
        static World world;

        public static void LoadWorldMap(string filename)
        {
            FileStream stream;
            // Check filecache
            if (FileCache.Instance.Contains(filename))
            {
                stream = File.OpenRead(FileCache.Instance.GetFile(filename).FullName);
            }
            // Check absolute path
            else if (File.Exists(filename))
            {
                stream = File.OpenRead(filename);
            }
            else
            {
                Common.Instance.Error(string.Format("LoadWorldMap: {0} not found", filename));
                return;
            }
            world = new World();

            BinaryReader br = new BinaryReader(stream, Encoding.UTF8);

            // Read header..
            int magic = (('P' << 24) + ('S' << 16) + ('B' << 8) + 'V');
            int id = br.ReadInt32();
            if (id != magic)
            {
                Common.Instance.Error(string.Format("LoadWorldMap:{0}: Wrong magic number", filename));
                return;
            }

            Header sHeader;
            sHeader.ident = id;
            sHeader.version = br.ReadInt32();
            sHeader.lumps = new Lump_t[Header.HEADER_LUMPS];
            for (int i = 0; i < Header.HEADER_LUMPS; i++)
            {
                Lump_t lump;
                lump.fileofs = br.ReadInt32();
                lump.filelen = br.ReadInt32();
                lump.version = br.ReadInt32();
                lump.fourCC = br.ReadChars(4);

                sHeader.lumps[i] = lump;
            }
            sHeader.mapRevision = br.ReadInt32();

            LoadLightGrids(br, sHeader);
            LoadLightmaps(br, sHeader);
            LoadPak(br, sHeader);
            LoadTextures(br, sHeader);
            LoadPlanes(br, sHeader);
            LoadDisplacement(br, sHeader);
            LoadFaces(br, sHeader); // Req: planes & texinfos & light & displacemtn
            LoadLeafBrushes(br, sHeader);
            LoadBrushes(br, sHeader);
            LoadLeafFaces(br, sHeader);
            LoadNodesAndLeafs(br, sHeader); // Req: planes
            LoadGameAndProps(br, sHeader); // Req: leafs
            LoadModels(br, sHeader);
            LoadEntities(br, sHeader);
            LoadVisibility(br, sHeader);

            SourceMap map = new SourceMap(world);
            map.Init();
            Renderer.Instance.SourceMap = map;

            //System.Console.WriteLine("#- Loaded Source BSP Map (" + (float)br.BaseStream.Length / 1024f / 1024f + "mb)");
            //System.Console.WriteLine("#-------------------------------");
            //System.Console.WriteLine("#\t Structure Load time:\t" + (float)(structureTime - materialTime - zipcacheTime) / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("#\t Zip Load time:\t\t\t" + (float)zipcacheTime / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("#\t Material Load time:\t" + (float)materialTime / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("#\t Lightmap Packing time:\t" + (float)texspan / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("#\t Displacement time:\t\t" + (float)diplaceTime / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("# Init time:\t\t\t" + (float)(initStart) / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("# Total File Load time:\t" + (float)(HighResolutionTimer.Ticks - startTime) / HighResolutionTimer.Frequency);
            //System.Console.WriteLine("#-------------------------------");
        }

        static void LoadBrushes(BinaryReader br, Header header)
        {
            // read brushsides
            br.BaseStream.Seek(header.lumps[19].fileofs, SeekOrigin.Begin);
            int numBrushSides = header.lumps[19].filelen / 8;
            for (int i = 0; i < numBrushSides; i++)
            {
                dbrushside_t brushside = new dbrushside_t();
                brushside.planenum = br.ReadUInt16();
                brushside.plane = world.planes[brushside.planenum];
                brushside.texinfo = br.ReadInt16();
                brushside.dispinfo = br.ReadInt16();
                brushside.bevel = br.ReadInt16();

                world.brushsides.Add(brushside);
            }

            // read brushes
            br.BaseStream.Seek(header.lumps[18].fileofs, SeekOrigin.Begin);
            //brushes = new dbrush_t[];
            int numBrushes = header.lumps[18].filelen / 12;
            for (int i = 0; i < numBrushes; i++)
            {
                dbrush_t brush = new dbrush_t();
                brush.firstside = br.ReadInt32();
                brush.numsides = br.ReadInt32();
                brush.sides = new dbrushside_t[brush.numsides];
                for (int j = 0; j < brush.numsides; j++)
                {
                    brush.sides[j] = world.brushsides[brush.firstside + j];
                }
                brush.contents = (brushflags)br.ReadInt32();
                brush.boundsmin = Vector3.Zero;
                brush.boundsmax = Vector3.Zero;
                brush.boundsmin[0] = -brush.sides[0].plane.dist;
                brush.boundsmax[0] = brush.sides[1].plane.dist;
                brush.boundsmin[1] = -brush.sides[2].plane.dist;
                brush.boundsmax[1] = brush.sides[3].plane.dist;
                brush.boundsmin[2] = -brush.sides[4].plane.dist;
                brush.boundsmax[2] = brush.sides[5].plane.dist;
                world.brushes.Add(brush);
            }
        }

        static void LoadModels(BinaryReader br, Header header)
        {
            // Models
            br.BaseStream.Seek(header.lumps[14].fileofs, SeekOrigin.Begin);
            int nModels = header.lumps[14].filelen / 48;
            dmodel_t[] models = new dmodel_t[nModels];
            for (int i = 0; i < nModels; i++)
            {
                dmodel_t model = new dmodel_t();
                model.mins = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                model.maxs = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                model.origin = SourceParser.SwapZY(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                model.headnode = br.ReadInt32();
                model.firstface = br.ReadInt32();
                model.numfaces = br.ReadInt32();
                models[i] = model;
            }

            world.cmodels = new cmodel_t[models.Length];

            for (int i = 0; i < models.Length; i++)
            {
                dmodel_t min = models[i];
                cmodel_t mout = new cmodel_t();
                mout.leaf = new dleaf_t();
                mout.mins = mout.maxs = Vector3.Zero;

                for (int j = 0; j < 3; j++)
                {
                    // spread the mins / maxs by a pixel
                    mout.mins[j] = min.mins[j] - 1;
                    mout.maxs[j] = min.maxs[j] + 1;
                }

                // world model doesn't need other info
                if (i == 0)
                    continue;

                mout.leaf.numleaffaces = (ushort)min.numfaces;
                mout.leaf.firstleafface = (ushort)world.leafFaces.Count;
                for (int j = 0; j < min.numfaces; j++)
                {
                    world.leafFaces.Add(min.firstface + j);
                }

                world.cmodels[i] = mout;
            }
        }

        static void LoadEntities(BinaryReader br, Header header)
        {
            // Entities
            br.BaseStream.Seek(header.lumps[0].fileofs, SeekOrigin.Begin);
            int nEntities = header.lumps[0].filelen/2;
            StringBuilder entitiesBuilder = new StringBuilder(nEntities);
            entitiesBuilder.Append(br.ReadChars(nEntities));
            world.EntityString = entitiesBuilder.ToString();
            //world.Entities = Entity.CreateEntities(world.EntityString);
        }

        static void LoadTextures(BinaryReader br, Header header)
        {
            // Read texdataStringTable
            br.BaseStream.Seek(header.lumps[44].fileofs, SeekOrigin.Begin);
            int nStringTable = header.lumps[44].filelen / sizeof(int);
            int[] StringTable = new int[nStringTable];
            for (int i = 0; i < nStringTable; i++)
            {
                StringTable[i] = br.ReadInt32();
            }


            // Read texture names
            StringBuilder strBuilder;
            string[] TextureNames = new string[nStringTable];
            char singleChar;
            for (int i = 0; i < nStringTable; i++)
            {
                strBuilder = new StringBuilder(64, 128);
                // Seek to string
                br.BaseStream.Seek(header.lumps[43].fileofs + StringTable[i], SeekOrigin.Begin);

                // Read until max lenght or \0 byte
                while ((singleChar = br.ReadChar()) != '\0' && strBuilder.Length < 128)
                {
                    strBuilder.Append(singleChar);
                }

                TextureNames[i] = strBuilder.ToString();
            }

            // Read texdata
            br.BaseStream.Seek(header.lumps[2].fileofs, SeekOrigin.Begin);
            int nTextdata = header.lumps[2].filelen / 32;
            texdata_t[] texdatas = new texdata_t[nTextdata];
            long materialTime = 0;
            for (int i = 0; i < nTextdata; i++)
            {

                texdata_t texdata;
                texdata.reflectivity = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                texdata.nameStringTableID = br.ReadInt32();
                texdata.name = TextureNames[texdata.nameStringTableID];
                texdata.width = br.ReadInt32();
                texdata.height = br.ReadInt32();
                texdata.view_width = br.ReadInt32();
                texdata.view_height = br.ReadInt32();
                long materialStart = HighResolutionTimer.Ticks;
                texdata.mat = TextureManager.Instance.LoadMaterial(texdata.name);
                materialTime += HighResolutionTimer.Ticks - materialStart;
                texdatas[i] = texdata;
            }

            // Read texinfo
            br.BaseStream.Seek(header.lumps[6].fileofs, SeekOrigin.Begin);
            int nTexinfo = header.lumps[6].filelen / 72;
            world.texinfos = new texinfo_t[nTexinfo];
            for (int i = 0; i < nTexinfo; i++)
            {
                // Init structure
                texinfo_t texinfo;
                texinfo.textureVecs = new Vector4[2];
                texinfo.lightmapVecs = new Vector4[2];

                // Read structure
                texinfo.textureVecs[0] = SwapZY(new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                texinfo.textureVecs[1] = SwapZY(new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                texinfo.lightmapVecs[0] = SwapZY(new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                texinfo.lightmapVecs[1] = SwapZY(new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                texinfo.flags = br.ReadInt32();
                texinfo.texdata = br.ReadInt32();
                texinfo.texdata_t = texdatas[texinfo.texdata];

                world.texinfos[i] = texinfo;
            }
        }

        static void LoadNodesAndLeafs(BinaryReader br, Header header)
        {
            // Read nodes
            br.BaseStream.Seek(header.lumps[5].fileofs, SeekOrigin.Begin);
            int nNodes = header.lumps[5].filelen / 32;
            if (header.lumps[5].filelen % 32 > 0)
            {
                Common.Instance.Error("LoadNodesAndLeafs: Weird node lumpsize");
            }
            world.nodes = new dnode_t[nNodes];
            for (int i = 0; i < nNodes; i++)
            {
                dnode_t node = new dnode_t();
                node.planenum = br.ReadInt32();
                node.children = new int[] { br.ReadInt32(), br.ReadInt32() };
                node.mins = SourceParser.SwapZY(new Vector3(br.ReadInt16(), br.ReadInt16(), br.ReadInt16())); // 3 For frustrum culling
                node.maxs = SourceParser.SwapZY(new Vector3(br.ReadInt16(), br.ReadInt16(), br.ReadInt16())); // 3
                node.firstface = br.ReadUInt16(); // index into face array
                node.numfaces = br.ReadUInt16(); ;  // counting both sides
                node.area = br.ReadInt16(); ;    // If all leaves below this node are in the same area, then
                // this is the area index. If not, this is -1.
                node.paddding = br.ReadInt16(); ;	 // pad to 32 bytes length

                node.plane = world.planes[node.planenum];

                world.nodes[i] = node;
            }

            // Determine size
            int leafSize = 56;
            if (header.version == 20 || header.version == 17)
            {
                if (header.lumps[10].filelen % 32 == 0)
                    leafSize = 32;
                else
                    System.Console.WriteLine("Problem reading leafs..");
            }

            world.numLeafs = header.lumps[10].filelen / leafSize;
            world.leafs = new dleaf_t[world.numLeafs];
            for (int i = 0; i < world.numLeafs; i++)
            {
                br.BaseStream.Seek(header.lumps[10].fileofs + (i * leafSize), SeekOrigin.Begin);
                dleaf_t leaf = new dleaf_t();
                leaf.contents = br.ReadInt32();
                leaf.cluster = br.ReadInt16();
                if (leaf.cluster > world.numClusters)
                    world.numClusters = leaf.cluster + 1;
                leaf.area = 0;//br.ReadInt16();
                leaf.flags = br.ReadInt16();
                leaf.mins = SourceParser.SwapZY(new Vector3(br.ReadInt16(), br.ReadInt16(), br.ReadInt16()));  // 3 For frustrum culling
                leaf.maxs = SourceParser.SwapZY(new Vector3(br.ReadInt16(), br.ReadInt16(), br.ReadInt16())); // 3

                leaf.firstleafface = br.ReadUInt16();
                leaf.numleaffaces = br.ReadUInt16();
                leaf.firstleafbrush = br.ReadUInt16();
                leaf.numleafbrushes = br.ReadUInt16();
                leaf.leafWaterDataID = br.ReadInt16();
                leaf.ambientLighting = new CompressedLightCube();
                if (leafSize > 32)
                {
                    leaf.ambientLighting.Color = new Vector3[6];
                    for (int j = 0; j < 6; j++)
                    {
                        RGBExp color = new RGBExp();
                        color.r = br.ReadByte();
                        color.g = br.ReadByte();
                        color.b = br.ReadByte();
                        color.exp = br.ReadSByte();
                        float r = SourceParser.TexLightToLinear((int)color.r, color.exp);
                        float g = SourceParser.TexLightToLinear((int)color.g, color.exp);
                        float b = SourceParser.TexLightToLinear((int)color.b, color.exp);
                        leaf.ambientLighting.Color[j] = new Vector3(r, g, b);
                    }
                }
                leaf.padding = 0;//br.ReadInt16();
                leaf.staticProps = new List<SourceProp>();
                world.leafs[i] = leaf;
            }
        }

        static void LoadLeafFaces(BinaryReader br, Header header)
        {
            // read leafFaces
            br.BaseStream.Seek(header.lumps[16].fileofs, SeekOrigin.Begin);
            world.leafFaces = new List<int>(header.lumps[16].filelen / 2);
            if (header.lumps[16].filelen % 2 > 0)
            {
                Common.Instance.Error("Weird leafFace lumpsize");
            }
            for (int i = 0; i < header.lumps[16].filelen / 2; i++)
            {
                world.leafFaces.Add(br.ReadUInt16());
            }
        }

        static void LoadLeafBrushes(BinaryReader br, Header header)
        {
            // read leafBrushes
            br.BaseStream.Seek(header.lumps[17].fileofs, SeekOrigin.Begin);
            int numLeafBrushes = header.lumps[17].filelen/2;
            if (header.lumps[17].filelen % 2 > 0)
            {
                Common.Instance.Error("LoadLeafBrushes: Weird leafBrush lumpsize");
            }
            world.leafbrushes = new List<int>();
            for (int i = 0; i < numLeafBrushes; i++)
            {
                world.leafbrushes.Add((int)br.ReadInt16());
            }
        }

        static void LoadFaces(BinaryReader br, Header header)
        {
            // Read vertexes..
            br.BaseStream.Seek(header.lumps[3].fileofs, SeekOrigin.Begin);
            int nVertexes = header.lumps[3].filelen / 12;
            Vector3[] loadedverts = new Vector3[nVertexes]; 
            for (int i = 0; i < nVertexes; i++)
            {
                Vector3 vec = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                // swap z and y axis
                vec = SwapZY(vec);
                loadedverts[i] = vec;
            }

            // Reade edge lump
            br.BaseStream.Seek(header.lumps[12].fileofs, SeekOrigin.Begin);
            int nEdges = header.lumps[12].filelen / (sizeof(short) * 2);
            edge_t[] edges = new edge_t[nEdges];
            for (int i = 0; i < nEdges; i++)
            {
                edge_t edge;
                edge.v = new ushort[] { br.ReadUInt16(), br.ReadUInt16() };
                edges[i] = edge;
            }

            // Read surfedges
            br.BaseStream.Seek(header.lumps[13].fileofs, SeekOrigin.Begin);
            int nSurfEdges = header.lumps[13].filelen / sizeof(int);
            int[] surfEdges = new int[nSurfEdges];
            for (int i = 0; i < nSurfEdges; i++)
            {
                surfEdges[i] = br.ReadInt32();
            }

            // Read faces
            br.BaseStream.Seek(header.lumps[7].fileofs, SeekOrigin.Begin);
            int nFaces = header.lumps[7].filelen / 56;
            if (header.lumps[7].filelen % 56 > 0)
            {
                Common.Instance.Error("Weird faces lumpsize");
            }
            world.faces_t = new face_t[nFaces];
            world.faces = new Face[world.faces_t.Length];
            for (int i = 0; i < nFaces; i++)
            {
                face_t face;
                face.planenum = br.ReadUInt16();
                face.side = br.ReadByte();
                face.onNode = br.ReadByte();
                face.firstedge = br.ReadInt32();
                face.numedges = br.ReadInt16();
                face.texinfo = br.ReadInt16();
                face.dispinfo = br.ReadInt16();
                face.surfaceFogVolumeID = br.ReadInt16();
                face.styles = br.ReadBytes(4);
                face.lightlumpofs = br.ReadInt32();
                if (face.lightlumpofs > -1)
                {
                    face.lightlumpofs /= 4; // From 4byte offset to 1color offset
                }
                face.area = br.ReadSingle();
                face.LightmapTextureMinsInLuxels = new int[] { br.ReadInt32(), br.ReadInt32() };
                face.LightmapTextureSizeInLuxels = new int[] { br.ReadInt32(), br.ReadInt32() };
                face.origFace = br.ReadInt32();
                face.numPrims = br.ReadUInt16();
                face.firstPrimID = br.ReadUInt16();
                face.smoothingGroups = br.ReadUInt32();
                

                // Prepare Faces structure
                Face face2 = new Face();
                face.face = face2;
                face2.face_t = face;

                face2.nVerts = face.numedges;
                face2.plane_t = world.planes[face.planenum];
                face2.texinfo = world.texinfos[face.texinfo];
                if ((face2.texinfo.flags & (SurfFlags.SURF_NODRAW | SurfFlags.SURF_HINT | SurfFlags.SURF_FLOWING | SurfFlags.SURF_NOLIGHT | SurfFlags.SURF_SKIP)) == 0)
                    nVertIndex += face.numedges;
                world.faces[i] = face2;
                world.faces_t[i] = face;
            }

            // Prepare lightmap texture
            Texture lightmapTexture = new Texture(Renderer.Instance.device, LightmapSize.Width, LightmapSize.Height, 1, Usage.None, Format.A16B16G16R16F, (Renderer.Instance.Is3D9Ex ? Pool.Default : Pool.Managed));
            DataRectangle textureData = lightmapTexture.LockRectangle(0, LockFlags.None);
            DataStream ds = textureData.Data;
            TextureNode texturePacker = new TextureNode(new System.Drawing.Rectangle(0, 0, LightmapSize.Width, LightmapSize.Height), null);
            TextureNode insertedNode = null; // TExtureNode result for each face

            int vertIndex = 0;
            world.verts = new List<VertexPositionNormalTexturedLightmap>();
            for (int i = 0; i < world.faces.Length; i++)
            {
                face_t face_t = world.faces[i].face_t;
                Face face = world.faces[i];

                if ((face.texinfo.flags & SurfFlags.SURF_NODRAW) > 0 ||
                    (face.texinfo.flags & SurfFlags.SURF_HINT) > 0 ||
                    (face.texinfo.flags & SurfFlags.SURF_FLOWING) > 0 ||
                    (face.texinfo.flags & SurfFlags.SURF_NOLIGHT) > 0 ||
                    (face.texinfo.flags & SurfFlags.SURF_SKIP) > 0)
                    continue; // Dont draw this

                // Add faces lightmap to lightmap texture
                int lmWidth = face_t.LightmapTextureSizeInLuxels[0] + 1;
                int lmHeight = face_t.LightmapTextureSizeInLuxels[1] + 1;
                int lmSize = lmWidth * lmHeight;
                for (int lightstyles = 0; lightstyles < 4; lightstyles++)
                {
                    if (face_t.styles[lightstyles] == 255)
                    {
                        continue;
                    }
                    if (face_t.lightlumpofs + (lmSize * lightstyles) >= world.LightData.Length || face_t.lightlumpofs == -1)
                    {
                        Common.Instance.WriteLine("WARNING: Face wants more lightdata than is available, or no offset");
                        break;
                    }
                    // Try to fit it in the texture..
                    insertedNode = texturePacker.Insert(ref world.LightData, lmWidth, lmHeight, face_t.lightlumpofs + (lmSize * lightstyles), ref ds, (face.face_t.dispinfo == -1 ? false : true));
                    if (insertedNode == null)
                        System.Console.WriteLine("Could not fit lightmap into a texture :( w:" + lmWidth + " h:" + lmHeight);
                    else// if (lightstyles == 0)
                    {
                        // Save lightmap coordinates
                        face.lightOffsetX = insertedNode.Rectangle.X;
                        face.lightOffsetY = insertedNode.Rectangle.Y;
                    }
                }

                // Build VertexPositionNormalTexturedLightmap's for face
                float s, t;
                int v;
                int pedge = face.face_t.firstedge;
                for (int j = 0;j < face.face_t.numedges; j++)
                {
                    int edge = surfEdges[pedge++];
                    if (edge < 0)
                    {
                        v = 1;
                        edge = -edge;
                    }
                    else
                        v = 0;

                    // Base Texture
                    Vector3 vert = loadedverts[edges[edge].v[v]];

                    // Texture coordinates
                    Vector3 texs = new Vector3(face.texinfo.textureVecs[0].X, face.texinfo.textureVecs[0].Y, face.texinfo.textureVecs[0].Z);
                    Vector3 text = new Vector3(face.texinfo.textureVecs[1].X, face.texinfo.textureVecs[1].Y, face.texinfo.textureVecs[1].Z);
                    s = (Vector3.Dot(vert, texs) + face.texinfo.textureVecs[0].W) / face.texinfo.texdata_t.width;
                    t = (Vector3.Dot(vert, text) + face.texinfo.textureVecs[1].W) / face.texinfo.texdata_t.height;

                    // Generate Lightmap Coordinates
                    float l_s = 0.5f, l_t = 0.5f;
                    if (face.face_t.LightmapTextureSizeInLuxels[0] != 0 && face.face_t.LightmapTextureSizeInLuxels[1] != 0)
                    {
                        Vector3 vecs = new Vector3(face.texinfo.lightmapVecs[0].X, face.texinfo.lightmapVecs[0].Y, face.texinfo.lightmapVecs[0].Z);
                        Vector3 vect = new Vector3(face.texinfo.lightmapVecs[1].X, face.texinfo.lightmapVecs[1].Y, face.texinfo.lightmapVecs[1].Z);

                        l_s = Vector3.Dot(vert, vecs) +
                            face.texinfo.lightmapVecs[0].W - face.face_t.LightmapTextureMinsInLuxels[0];
                        l_s /= face.face_t.LightmapTextureSizeInLuxels[0];

                        l_t = Vector3.Dot(vert, vect) +
                            face.texinfo.lightmapVecs[1].W - face.face_t.LightmapTextureMinsInLuxels[1];
                        l_t /= face.face_t.LightmapTextureSizeInLuxels[1];

                        float divx = (float)(face.face_t.LightmapTextureSizeInLuxels[0]) / (LightmapSize.Width);
                        float startx = (float)(face.lightOffsetX + 0.5f) / (LightmapSize.Width);
                        l_s = divx * l_s + startx;

                        float divy = (float)(face.face_t.LightmapTextureSizeInLuxels[1]) / (LightmapSize.Height);
                        float starty = (float)(face.lightOffsetY + 0.5f) / (LightmapSize.Height);
                        l_t = divy * l_t + starty;
                    }

                    // Set vertex offset for face
                    if (face.VertexOffset == -1)
                        face.VertexOffset = vertIndex;

                    world.verts.Add(new VertexPositionNormalTexturedLightmap(vert, face.plane_t.normal, new Vector2(s, t), new Vector2(l_s, l_t)));
                    vertIndex++;
                }

                // Handle displacement face
                if (face.face_t.dispinfo != -1)
                {
                    

                    // Get the displacement info
                    ddispinfo_t currentDispInfo = world.ddispinfos[face.face_t.dispinfo];

                    int faceIndex = currentDispInfo.MapFace;
                    while (true)
                    {
                        if (world.faces_t.Length <= faceIndex)
                            //break;

                        if (world.faces_t[faceIndex].origFace > 0 && world.faces_t[faceIndex].origFace != faceIndex)
                        {
                            faceIndex = world.faces_t[faceIndex].origFace;
                            //continue;
                        }
                        faceIndex = 0;
                        if (world.faces_t[faceIndex].face.DisplaceFaces == null)
                            world.faces_t[faceIndex].face.DisplaceFaces = new int[] { i };
                        else
                        {
                            int[] arr = new int[world.faces_t[faceIndex].face.DisplaceFaces.Length+1];
                            world.faces_t[faceIndex].face.DisplaceFaces.CopyTo(arr, 0);
                            arr[arr.Length - 1] = i;
                            world.faces_t[faceIndex].face.DisplaceFaces = arr;
                        }
                        world.faces_t[faceIndex].face.HasDisplacement = true;
                        break;
                    }

                    // Generate the displacement surface
                    createDispSurface(face, currentDispInfo, world);
                }
            }


            lightmapTexture.UnlockRectangle(0);
            world.LightmapTexture = lightmapTexture;
        }

        static void LoadVisibility(BinaryReader br, Header header)
        {
            // Read Vis
            br.BaseStream.Seek(header.lumps[4].fileofs, SeekOrigin.Begin);
            int nVis = header.lumps[4].filelen / 68;
            if (nVis > 0)
            {
                world.vis = new dvis_t();
                world.vis.numclusters = br.ReadInt32();
                world.vis.byteofs = new int[world.vis.numclusters][];
                for (int j = 0; j < world.vis.numclusters; j++)
                {
                    world.vis.byteofs[j] = new int[] { br.ReadInt32(), br.ReadInt32() };
                }
                world.numClusters = world.vis.numclusters;
                // VisData
                br.BaseStream.Seek(header.lumps[4].fileofs, SeekOrigin.Begin);
                int nVisData = (int)(header.lumps[4].filelen);
                world.visibility = new byte[nVisData];
                for (int i = 0; i < nVisData; i++)
                {
                    world.visibility[i] = br.ReadByte();
                }
                world.vised = true;
            }
            else
            {
                int clusterBytes = (world.numClusters + 31) & ~31;
                world.visibility = new byte[clusterBytes];
                for (int i = 0; i < clusterBytes; i++)
                {
                    world.visibility[i] = 255;
                }
            }
        }

        static void createDispSurface(Face face, ddispinfo_t dispInfo, World map)
        {
            face.HasDisplacement = true;
            face.displace_offset = world.verts.Count;

            //// Get the texture vectors and offsets.  These are used to calculate
            //// texture coordinates 
            Vector3 texU = new Vector3(face.texinfo.textureVecs[0].X,
                     face.texinfo.textureVecs[0].Y,
                     face.texinfo.textureVecs[0].Z);
            float texUOffset = face.texinfo.textureVecs[0].W;

            Vector3 texV = new Vector3(face.texinfo.textureVecs[1].X,
                     face.texinfo.textureVecs[1].Y,
                     face.texinfo.textureVecs[1].Z);
            float texVOffset = face.texinfo.textureVecs[1].W;

            // Get the base vertices for this face
            Vector3[] vertices = new Vector3[face.nVerts];
            for (int i = 0; i < face.nVerts; i++)
            {
                vertices[i] = world.verts[face.VertexOffset + i].position;
            }

            // Rotate the base coordinates for the surface until the first vertex
            // matches the start position
            float minDist = float.MaxValue;
            int minIndex = 0;
            for (int i = 0; i < 4; i++)
            {
                // Calculate the distance of the start position from this vertex
                float dist = (world.verts[face.VertexOffset + i].position - dispInfo.startPosition).Length();// * 0.0254f).Length();

                // If this is the smallest distance we've seen, remember it
                if (dist < minDist)
                {
                    minDist = dist;
                    minIndex = i;
                }
            }

            // Rotate the displacement surface quad until we get the starting vertex
            // in the 0th position
            for (int i = 0; i < minIndex; i++)
            {
                Vector3 temp = vertices[0];
                vertices[0] = vertices[1];
                vertices[1] = vertices[2];
                vertices[2] = vertices[3];
                vertices[3] = temp;
            }

            // Calculate the vectors for the left and right edges of the surface
            // (remembering that the surface is wound clockwise)
            Vector3 leftEdge = vertices[1] - vertices[0];
            Vector3 rightEdge = vertices[2] - vertices[3];

            // Calculate the number of vertices along each edge of the surface
            int numEdgeVertices = (1 << dispInfo.power) + 1;

            // Calculate the subdivide scale, which will tell us how far apart to
            // put each vertex (relative to the length of the surface's edges)
            double subdivideScale = 1.0f / (double)(numEdgeVertices - 1);

            // Calculate the step size between vertices on the left and right edges
            Vector3 leftEdgeStep = Vector3.Multiply(leftEdge, (float)subdivideScale);
            Vector3 rightEdgeStep = Vector3.Multiply(rightEdge, (float)subdivideScale);

            // Remember the first vertex index in the vertex array
            uint firstVertex = (uint)world.verts.Count;

            float lightdeltaU = (1f) / (numEdgeVertices - 1);
            float lightdeltaV = (1f) / (numEdgeVertices - 1);

            float texUScale = 1.0f / (float)face.texinfo.texdata_t.width;
            float texVScale = 1.0f / (float)face.texinfo.texdata_t.height;
            
            // Temporary lists for accumulating the pars of a full VertexPositionTexturedNormalLightmap
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> texcoords = new List<Vector2>();
            List<Vector2> lightcoords = new List<Vector2>();

            // Generate the displaced vertices (this technique comes from the
            // Source SDK)
            for (int i = 0; i < numEdgeVertices; i++)
            {
                // Calculate the two endpoints for this section of the surface
                Vector3 leftEnd = Vector3.Multiply(leftEdgeStep, i);
                leftEnd += vertices[0];
                Vector3 rightEnd = Vector3.Multiply(rightEdgeStep, i);
                rightEnd += vertices[3];

                // Now, get the vector from left to right, and subdivide it as well
                Vector3 leftRightSeg = rightEnd - leftEnd;
                Vector3 leftRightStep = Vector3.Multiply(leftRightSeg, (float)subdivideScale);

                // Generate the vertices for this section
                for (int j = 0; j < numEdgeVertices; j++)
                {
                    // Get the displacement info for this vertex
                    uint dispVertIndex = (uint)Math.Abs(dispInfo.DispVertStart);
                    dispVertIndex += (uint)(i * numEdgeVertices + j);
                    dDispVert dispVertInfo = world.dispVerts[(int)dispVertIndex];

                    // Calculate the flat vertex
                    Vector3 flatVertex = leftRightStep;
                    flatVertex = Vector3.Multiply(flatVertex, j) + leftEnd;

                    // Calculate the displaced vertex
                    Vector3 dispVertex = dispVertInfo.vec;
                    dispVertex = Vector3.Multiply(dispVertex, (float)(dispVertInfo.dist)) + flatVertex;
                    verts.Add(dispVertex);

                    // Calculate the texture coordinates for this vertex.  Texture
                    // coordinates are calculated using a planar projection, so we need
                    // to use the non-displaced vertex position here
                    float u = Vector3.Dot(texU, flatVertex) + texUOffset;
                    u *= texUScale;
                    float v = Vector3.Dot(texV, flatVertex) + texVOffset;
                    v *= texVScale;
                    Vector2 texCoord = new Vector2(u, v);
                    texcoords.Add(texCoord);

                    // Generate lightmap coordinates
                    float lightmapU = (lightdeltaU * j * face.face_t.LightmapTextureSizeInLuxels[0]) + face.lightOffsetX + 0.5f; // pixel space
                    float lightmapV = (lightdeltaV * i * face.face_t.LightmapTextureSizeInLuxels[1]) + face.lightOffsetY + 0.5f; // pixel space
                    lightmapU /= LightmapSize.Width;
                    lightmapV /= LightmapSize.Height;
                    lightcoords.Add(new Vector2(lightmapU, lightmapV));

                    // Get the texture blend parameter for this vertex as well
                    //float eh = (float)(dispVertInfo.alpha / 255.0);
                }
            }

            List<Vector3> normals = new List<Vector3>();
            // Calculate normals at each vertex (this is adapted from the Source SDK,
            // including the two helper functions)
            for (int i = 0; i < numEdgeVertices; i++)
            {
                for (int j = 0; j < numEdgeVertices; j++)
                {
                    // See which of the 4 possible edges (left, up, right, or down) are
                    // incident on this vertex
                    byte edgeBits = 0;
                    for (int k = 0; k < 4; k++)
                    {
                        if (doesEdgeExist(j, i, (int)k, numEdgeVertices))
                            edgeBits |= (byte)(1 << (byte)k);
                    }

                    // Calculate the normal based on the adjacent edges
                    Vector3 normal = getNormalFromEdges(j, i, edgeBits,
                                                numEdgeVertices, verts);

                    // Add the normal to the normal array
                    normals.Add(normal);
                }
            }

            // Build real vertices
            for (int i = 0; i < verts.Count; i++)
            {
                VertexPositionNormalTexturedLightmap vert = new VertexPositionNormalTexturedLightmap(verts[i], normals[i], texcoords[i], lightcoords[i]);
                world.verts.Add(vert);
            }

            // Build indices
            List<uint> indices = new List<uint>();
            // Now, triangulate the surface (this technique comes from the Source SDK)
            for (int i = 0; i < numEdgeVertices - 1; i++)
            {
                for (int j = 0; j < numEdgeVertices - 1; j++)
                {
                    // Get the current vertex index (local to this surface)
                    uint index = (uint)(i * numEdgeVertices + j);
                    //if (index + numEdgeVertices + 1 + firstVertex > map.disp_vertex_array.Count - 1 || index + firstVertex < firstVertex)
                    //{
                    //    int test = 2;
                    //}
                    // See if this index is odd
                    if ((index % 2) == 1)
                    {
                        // Add the vertex offset (so we reference this surface's
                        // vertices in the array)
                        index += firstVertex;

                        // Create two triangles on this vertex from top-left to
                        // bottom-right
                        indices.Add((uint) index + 1);
                        indices.Add((uint) index);

                        indices.Add((uint) index + (uint)numEdgeVertices);
                        indices.Add((uint) index + (uint)numEdgeVertices + 1);
                        indices.Add((uint) index + 1);

                        indices.Add((uint)index + (uint)numEdgeVertices);
                    }
                    else
                    {
                        // Add the vertex offset (so we reference this surface's
                        // vertices in the array)
                        index += firstVertex;

                        // Create two triangles on this vertex from bottom-left to
                        // top-right
                        indices.Add((uint) index + (uint)numEdgeVertices + 1);
                        indices.Add((uint)index);

                        indices.Add((uint)index + (uint)numEdgeVertices);
                        indices.Add((uint)index + 1);
                        indices.Add((uint) index);

                        indices.Add((uint)index + (uint)numEdgeVertices + 1);
                    }
                }
                //face.VertexOffset = firstVertex;

            }

            face.nVerts = world.verts.Count - (int)firstVertex;
            face.indices = indices.ToArray();
            //face.nDisplace = map.disp_primitive_set.Count - face.displace_offset;
        }

        static void LoadPlanes(BinaryReader br, Header header)
        {
            // Read planes
            br.BaseStream.Seek(header.lumps[1].fileofs, SeekOrigin.Begin);
            int numPlanes = header.lumps[1].filelen / 20;
            if (header.lumps[1].filelen % 20 > 0)
            {
                Common.Instance.Error("LoadPlanes: Weird plane lumpsize");
            }
            for (int i = 0; i < numPlanes; i++)
            {
                cplane_t plane = new cplane_t();
                plane.normal = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                plane.dist = br.ReadSingle();
                plane.type = br.ReadInt32();
                plane.type = plane.normal[0] == 1.0 ? 0 : (plane.normal[1] == 1.0 ? 1 : (plane.normal[2] == 1.0 ? 2 : 3));
                int bits = 0;
                for (int j = 0; j < 3; j++)
                {
                    if (plane.normal[j] < 0.0f)
                        bits |= 1 << j;
                }
                plane.signbits = (byte)bits;
                world.planes.Add(plane);
            }
        }

        static void LoadLightmaps(BinaryReader br, Header header)
        {
            // Read Lightmaps
            br.BaseStream.Seek(header.lumps[8].fileofs, SeekOrigin.Begin);
            int nLightmaps = header.lumps[8].filelen / 4;
            Color3[] Lightmap = new Color3[nLightmaps];
            RGBExp[] LightmapColors = new RGBExp[nLightmaps];
            for (int i = 0; i < nLightmaps; i++)
            {
                LightmapColors[i] = new RGBExp();
                LightmapColors[i].r = br.ReadByte();
                LightmapColors[i].g = br.ReadByte();
                LightmapColors[i].b = br.ReadByte();
                LightmapColors[i].exp = br.ReadSByte() ;
                float r = TexLightToLinear((int)LightmapColors[i].r, LightmapColors[i].exp);
                float g = TexLightToLinear((int)LightmapColors[i].g, LightmapColors[i].exp);
                float b = TexLightToLinear((int)LightmapColors[i].b, LightmapColors[i].exp);
                float exp = 128 + (int)LightmapColors[i].exp;
                Lightmap[i] = new Color3(r, g, b);
            }
            world.LightData = Lightmap;
        }

        static void LoadLightGrids(BinaryReader br, Header header)
        {
            // HDR Ambient light may be in lump for itself
            if (header.lumps[55].filelen > 0)
            {
                int nHDRAmbient = header.lumps[55].filelen / 24;
                CompressedLightCube[] HDRCubes = new CompressedLightCube[nHDRAmbient];
                br.BaseStream.Seek(header.lumps[55].fileofs, SeekOrigin.Begin);
                for (int i = 0; i < nHDRAmbient; i++)
                {
                    CompressedLightCube cube = new CompressedLightCube();
                    cube.Color = new Vector3[6];
                    for (int j = 0; j < 6; j++)
                    {
                        RGBExp color = new RGBExp();
                        color.r = br.ReadByte();
                        color.g = br.ReadByte();
                        color.b = br.ReadByte();
                        color.exp = br.ReadSByte();

                        float r = TexLightToLinear((int)color.r, color.exp) * 255;
                        float g = TexLightToLinear((int)color.g, color.exp) * 255;
                        float b = TexLightToLinear((int)color.b, color.exp) * 255;
                        float exp = 128 + (int)color.exp;

                        cube.Color[j] = new Vector3(r, g, b);
                    }
                    HDRCubes[i] = cube;
                }
                world.LightGrid = HDRCubes;
            }
            // LDR Ambient light may be in lump for itself
            else if (header.lumps[56].filelen > 0)
            {
                int nLDRAmbient = header.lumps[56].filelen / 24;
                CompressedLightCube[] LDRCubes = new CompressedLightCube[nLDRAmbient];
                br.BaseStream.Seek(header.lumps[56].fileofs, SeekOrigin.Begin);
                for (int i = 0; i < nLDRAmbient; i++)
                {
                    CompressedLightCube cube = new CompressedLightCube();
                    cube.Color = new Vector3[6];
                    for (int j = 0; j < 6; j++)
                    {
                        RGBExp color = new RGBExp();
                        color.r = br.ReadByte();
                        color.g = br.ReadByte();
                        color.b = br.ReadByte();
                        color.exp = br.ReadSByte();

                        float r = TexLightToLinear((int)color.r, color.exp) * 255;
                        float g = TexLightToLinear((int)color.g, color.exp) * 255;
                        float b = TexLightToLinear((int)color.b, color.exp) * 255;
                        float exp = 128 + (int)color.exp;
                        cube.Color[j] = new Vector3(r, g, b);
                    }
                    LDRCubes[i] = cube;
                }
                world.LightGrid = LDRCubes;
            }
        }

        static void LoadDisplacement(BinaryReader br, Header header)
        {
            // Read DispInfo
            br.BaseStream.Seek(header.lumps[26].fileofs, SeekOrigin.Begin);
            int nDispInfo = header.lumps[26].filelen / 176;
            ddispinfo_t[] ddispinfos = new ddispinfo_t[nDispInfo];
            for (int i = 0; i < nDispInfo; i++)
            {
                br.BaseStream.Seek(header.lumps[26].fileofs + (i * 176), SeekOrigin.Begin);
                ddispinfo_t info = new ddispinfo_t();
                info.startPosition = SwapZY(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                info.DispVertStart = (int)br.ReadUInt32();
                info.DispTriStart = br.ReadInt32();
                info.power = br.ReadInt32();
                info.minTess = br.ReadInt32();
                info.smoothingAngle = br.ReadSingle();
                info.contents = br.ReadInt32();
                info.MapFace = br.ReadUInt16();

                

                info.LightmapAlphaStart = br.ReadInt32();
                info.LightmapSamplePositionStart = br.ReadInt32();

                info.EdgeNeighbors = new DisplaceNeighbor[4];
                for (int j = 0; j < 4; j++)
                {
                    DisplaceNeighbor dispNei = new DisplaceNeighbor();
                    dispNei.sub_neighbors = new DisplaceSubNeighbor[2];
                    for (int h = 0; h < 2; h++)
                    {
                        DisplaceSubNeighbor subNei = new DisplaceSubNeighbor();
                        subNei.neighbor_index = br.ReadUInt16();
                        subNei.neighbor_orient = br.ReadByte();
                        subNei.local_span = br.ReadByte();
                        subNei.neighbor_span = br.ReadByte();

                        dispNei.sub_neighbors[h] = subNei;
                    }

                    info.EdgeNeighbors[j] = dispNei;
                }
                info.CornerNeighbors = new DisplaceCornerNeighbor[4];
                for (int j = 0; j < 4; j++)
                {
                    DisplaceCornerNeighbor corner = new DisplaceCornerNeighbor();
                    corner.neighbor_indices = new ushort[] { br.ReadUInt16(), br.ReadUInt16(), br.ReadUInt16(), br.ReadUInt16() }; // 4
                    corner.neighbor_count = br.ReadByte();
                    info.CornerNeighbors[j] = corner;
                }
                info.AllowedVerts = new ulong[10];
                for (int j = 0; j < info.AllowedVerts.Length; j++)
                {
                    info.AllowedVerts[j] = br.ReadUInt64();
                }

                ddispinfos[i] = info;
            }
            world.ddispinfos = ddispinfos;

            // Read DispLIghtmapSamples
            br.BaseStream.Seek(header.lumps[34].fileofs, SeekOrigin.Begin);
            int nSamples = header.lumps[34].filelen;
            byte[] dispLightmapSamples = new byte[nSamples];
            for (int i = 0; i < nSamples; i++)
            {
                dispLightmapSamples[i] = br.ReadByte();
            }
            world.dispLightmapSamples = dispLightmapSamples;
            
            // Read DispVerts
            br.BaseStream.Seek(header.lumps[33].fileofs, SeekOrigin.Begin);
            int nDispVerts = header.lumps[33].filelen / 20;
            dDispVert[] dispVerts = new dDispVert[nDispVerts];
            for (int i = 0; i < nDispVerts; i++)
            {
                dDispVert vert = new dDispVert();
                vert.vec = SwapZY(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                vert.dist = br.ReadSingle();
                vert.alpha = br.ReadSingle();
                dispVerts[i] = vert;
            }
            world.dispVerts = dispVerts;
        }

        static void LoadPak(BinaryReader br, Header header)
        {
            // Read paklump
            br.BaseStream.Seek(header.lumps[40].fileofs, SeekOrigin.Begin);
            int nbytes = header.lumps[40].filelen;
            // Read uncompressed zip file
            byte[] paklump = br.ReadBytes(nbytes);
            ZipFile zip = ZipFile.Read(paklump);

            // Cache it
            TextureManager.Instance.CacheZipFile(zip);
            world.Pakfile = zip;
        }

        static void LoadGameAndProps(BinaryReader br, Header header)
        {
            // Game Lump
            dgamelumpheader_t gameheader = new dgamelumpheader_t();
            br.BaseStream.Seek(header.lumps[35].fileofs, SeekOrigin.Begin);
            int gamelen = header.lumps[35].filelen;
            gameheader.lumpCount = br.ReadInt32();
            gameheader.gamelump = new dgamelump_t[gameheader.lumpCount];
            for (int i = 0; i < gameheader.lumpCount; i++)
            {
                dgamelump_t game = new dgamelump_t();
                game.id = br.ReadInt32();
                game.flags = br.ReadUInt16();
                game.version = br.ReadUInt16();
                game.fileofs = br.ReadInt32();
                game.filelen = br.ReadInt32();

                gameheader.gamelump[i] = game;
            }

            // Read Static Props
            foreach (dgamelump_t game in gameheader.gamelump)
            {
                if (game.id == 1936749168)
                {
                    // read prop dict
                    StaticPropDictLump_t dict = new StaticPropDictLump_t();
                    br.BaseStream.Seek(game.fileofs, SeekOrigin.Begin);
                    dict.dictEntries = br.ReadInt32();
                    dict.Name = new string[dict.dictEntries];
                    Dictionary<string, SourceModel> srcmodels = new Dictionary<string, SourceModel>();
                    for (int i = 0; i < dict.dictEntries; i++)
                    {
                        char[] name = br.ReadChars(128);
                        dict.Name[i] = new string(name);
                        // cut \0 chars
                        int dindex = -1;
                        if ((dindex = dict.Name[i].IndexOf('\0')) != -1)
                        {
                            dict.Name[i] = dict.Name[i].Substring(0, dindex);
                            if (MDLReader.ReadFile(dict.Name[i]))
                            {
                                MDLReader.srcModel.Name = dict.Name[i];
                                srcmodels.Add(MDLReader.srcModel.Name, MDLReader.srcModel);
                            }
                        }
                    }

                    // Read prop leafs
                    StaticPropLeafLump_t propleaf = new StaticPropLeafLump_t();
                    propleaf.leafEntries = br.ReadInt32();
                    propleaf.leaf = new ushort[propleaf.leafEntries];
                    for (int i = 0; i < propleaf.leafEntries; i++)
                    {
                        propleaf.leaf[i] = br.ReadUInt16();
                    }
                    world.propleafs = propleaf;

                    // read props
                    int nStaticProps = br.ReadInt32();
                    StaticPropLump_t[] props = new StaticPropLump_t[nStaticProps];
                    for (int i = 0; i < nStaticProps; i++)
                    {
                        StaticPropLump_t prop = new StaticPropLump_t();
                        prop.Origin = SwapZY(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                        prop.Angles = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                        prop.PropType = br.ReadUInt16();
                        prop.PropName = dict.Name[(int)prop.PropType];
                        prop.FirstLeaf = br.ReadUInt16();
                        prop.LeafCount = br.ReadUInt16();
                        prop.Solid = br.ReadByte();
                        prop.Flags = br.ReadByte();
                        prop.Skin = br.ReadInt32();
                        prop.FadeMinDist = br.ReadSingle();
                        prop.FadeMaxDist = br.ReadSingle();
                        prop.LightingOrigin = SwapZY(new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()));
                        if (game.version == 5)
                            prop.ForcedFaceScale = br.ReadSingle();
                        else
                            prop.ForcedFaceScale = 1f;

                        props[i] = prop;
                        SourceProp sprop = new SourceProp(prop);
                        if (srcmodels.ContainsKey(prop.PropName))
                        {
                            sprop.srcModel = srcmodels[prop.PropName];
                            world.sourceProps.Add(sprop);
                        }

                        // Index into leaves
                        if (prop.LeafCount + prop.FirstLeaf < world.propleafs.leafEntries)
                        {
                            for (int j = 0; j < prop.LeafCount; j++)
                            {
                                ushort leafIndex = world.propleafs.leaf[prop.FirstLeaf + j];
                                if (leafIndex >= world.leafs.Length)
                                {
                                    int test = 2;
                                }
                                world.leafs[leafIndex].staticProps.Add(sprop);
                            }
                        }
                    }
                    world.props = props;

                    break;
                }
            }
        }

       

        public static float TexLightToLinear( int c, int exponent )
        { 
	        //Assert( exponent >= -128 && exponent <= 127 );
            return c * (float)Math.Pow(2, exponent);
            if (exponent >= -128 && exponent <= 127)
                return (float)c * power2_n[exponent + 128];
            else return 0f;
        }

        private static float Clamp(double input, float min, float max)
        {
            if (input > max)
                return (float)max;
            else if (input < min)
                return (float)min;
            else
                return (float)input;
        }

        public static Vector3 SwapZY(Vector3 vec)
        {
            //float temp = vec.Y;
            //vec.Y = vec.Z;
            //vec.Z = -temp;
            //vec.X = -vec.X;
            return vec;
        }

        public static Vector3 UnSwapZY(Vector3 vec)
        {
            //float temp = vec.Y;
            //vec.Y = -vec.Z;
            //vec.Z = temp;
            //vec.X = -vec.X;
            return vec;
        }

        public static Vector4 SwapZY(Vector4 vec)
        {
            //float temp = vec.Y;
            //vec.Y = vec.Z;
            //vec.Z = -temp;
            //vec.X = -vec.X;
            return vec;
        }

        

        static public bool doesEdgeExist(int row, int col, int direction, int vertsPerEdge)
        {
            // See if there is an edge on the displacement surface from the given
            // vertex in the given direction (we only need to know the vertices
            // indices, because all displacement surfaces are tessellated in the
            // same way)
            switch (direction)
            {
                case 0:
                    // False if we're on the left edge, otherwise true
                    if ((row - 1) < 0)
                        return false;
                    else
                        return true;

                case 1:
                    // False if we're on the top edge, otherwise true
                    if ((col + 1) >= vertsPerEdge)
                        return false;
                    else
                        return true;

                case 2:
                    // False if we're on the right edge, otherwise true
                    if ((row + 1) >= vertsPerEdge)
                        return false;
                    else
                        return true;

                case 3:
                    // False if we're on the bottom edge, otherwise true
                    if ((col - 1) < 0)
                        return false;
                    else
                        return true;

                default:
                    return false;
            }
        }


        static public Vector3 getNormalFromEdges(int row, int col, byte edgeBits, int vertsPerEdge, List<Vector3> verts)
        {

            Vector3 finalNormal;
            Vector3 v1, v2, v3;
            Vector3 e1, e2;
            Vector3 tempNormal;
            int normalCount;

            // Constants for direction.  If the bit is set in the edgeBits, then
            // there is an edge connected to the current vertex in that direction
            const byte NEG_X = 1 << 0;
            const byte POS_Y = 1 << 1;
            const byte POS_X = 1 << 2;
            const byte NEG_Y = 1 << 3;

            // Constants for quadrants.  If both bits are set, then there are
            // exactly two triangles in that quadrant
            const byte QUAD_1 = POS_X | POS_Y;
            const byte QUAD_2 = NEG_X | POS_Y;
            const byte QUAD_3 = NEG_X | NEG_Y;
            const byte QUAD_4 = POS_X | NEG_Y;

            // Grab the vertex data from the displaced vertex array (if there's a
            // better way to randomly access the data in this array, I'm all ears)


            // Move to the surface we're interested in, and start counting vertices
            // from there
            //surfaceVerts = disp_vertex_array[firstVertex + ];

            // Start with no normals computed
            finalNormal = new Vector3(0.0f, 0.0f, 0.0f);
            normalCount = 0;

            // The process is fairly simple.  For all four quadrants surrounding
            // the vertex, check each quadrant to see if there are triangles there.
            // If so, calculate the normals of the two triangles in that quadrant, and
            // add them to the final normal.  When fininshed, scale the final normal
            // based on the number of contributing triangle normals

            // Check quadrant 1 (+X,+Y)
            if ((edgeBits & QUAD_1) == QUAD_1)
            {
                // First triangle
                v1 = verts[  (col + 1) * vertsPerEdge + row];
                v2 = verts[  col * vertsPerEdge + row];
                v3 = verts[  col * vertsPerEdge + (row + 1)];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;

                // Second triangle
                v1 = verts[  (col + 1) * vertsPerEdge + row];
                v2 = verts[  col * vertsPerEdge + (row + 1)];
                v3 = verts[  (col + 1) * vertsPerEdge + (row + 1)];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;
            }

            // Check quadrant 2 (-X,+Y)
            if ((edgeBits & QUAD_2) == QUAD_2)
            {
                // First triangle
                v1 = verts[  (col + 1) * vertsPerEdge + (row - 1)];
                v2 = verts[  col * vertsPerEdge + (row - 1)];
                v3 = verts[  col * vertsPerEdge + row];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;

                // Second triangle
                v1 = verts[  (col + 1) * vertsPerEdge + (row - 1)];
                v2 = verts[  col * vertsPerEdge + row];
                v3 = verts[  (col + 1) * vertsPerEdge + row];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;
            }

            // Check quadrant 3 (-X,-Y)
            if ((edgeBits & QUAD_3) == QUAD_3)
            {
                // First triangle
                v1 = verts[  col * vertsPerEdge + (row - 1)];
                v2 = verts[  (col - 1) * vertsPerEdge + (row - 1)];
                v3 = verts[  (col - 1) * vertsPerEdge + row];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;

                // Second triangle
                v1 = verts[  col * vertsPerEdge + (row - 1)];
                v2 = verts[  (col - 1) * vertsPerEdge + row];
                v3 = verts[  col * vertsPerEdge + row];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;
            }

            // Check quadrant 4 (+X,-Y)
            if ((edgeBits & QUAD_4) == QUAD_4)
            {
                // First triangle
                v1 = verts[  col * vertsPerEdge + row];
                v2 = verts[  (col - 1) * vertsPerEdge + row];
                v3 = verts[  (col - 1) * vertsPerEdge + (row + 1)];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;

                // Second triangle
                v1 = verts[  col * vertsPerEdge + row];
                v2 = verts[  (col - 1) * vertsPerEdge + (row + 1)];
                v3 = verts[  col * vertsPerEdge + (row + 1)];
                e1 = v1 - v2;
                e2 = v3 - v2;
                tempNormal = Vector3.Cross(e2, e1);
                tempNormal.Normalize();
                finalNormal += tempNormal;
                normalCount++;
            }

            // Scale the final normal according to how many triangle normals are
            // contributing
            finalNormal *= (1.0f / (float)normalCount);

            return finalNormal;
        }

        public static float[] GetBarycentricCoords2D(Vector2 a, Vector2 b, Vector2 c, Vector2 pt)
        {
            float invTriArea = 1f / TriArea2DTimesTwo(a, b, c);

            float[] bcCoords = new float[3];
            bcCoords[0] = TriArea2DTimesTwo(b, c, pt) * invTriArea;
            bcCoords[1] = TriArea2DTimesTwo(c, a, pt) * invTriArea;
            bcCoords[2] = TriArea2DTimesTwo(a, b, pt) * invTriArea;

            return bcCoords;
        }

        public static float TriArea2DTimesTwo(Vector2 A,Vector2 B,Vector2 C ) {
            return ((B.X - A.X) * (C.Y - A.Y) - (B.Y - A.Y) * (C.X - A.X));
        }
    }
}
