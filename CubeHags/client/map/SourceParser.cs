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
using CubeHags.client.map;
using CubeHags.client.render;

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
        public dDispTri[] dispTris;

        public List<VertexPositionNormalTexturedLightmap> verts = new List<VertexPositionNormalTexturedLightmap>();
        public Face[] faces;
        public face_t[] faces_t;
        public texinfo_t[] texinfos;
        public Texture LightmapTexture;
        public ZipFile Pakfile;
        public List<Entity> Entities;
        public cmodel_t[] cmodels;
        public worldlight_t[] worldlights;

        public StaticPropLeafLump_t propleafs;
        public StaticPropLump_t[] props;
        public List<SourceProp> sourceProps = new List<SourceProp>();

        public List<int> leafFaces = new List<int>();
        public List<dbrushside_t> brushsides = new List<dbrushside_t>();
        public List<dbrush_t> brushes = new List<dbrush_t>();
        public edge_t[] edges;
        public int[] DispIndexToFaceIndex;

        public DispCollTree[] dispCollTrees;
        public int[] surfEdges;

        public int numClusters;
        public int clusterBytes;
        public byte[] visibility;
        public dvis_t vis;
        public bool vised;			// if false, visibility is just a single cluster of ffs

        public string EntityString;
        public int EntityParsePoint;        
    }
    public sealed class SourceParser
    {
        private static readonly SourceParser _Instance = new SourceParser();
        public static SourceParser Instance { get { return _Instance; } }
        static Size LightmapSize = new Size(1024, 1024);
        static float[]	power2_n = new float[256];
        static double[] g_aPowsOfTwo = new double[257];
        static int nVertIndex = 0;
        public static int ALLOWEDVERTS = 289;
        public static World world;
        public float[] lineartovertex = new float[4096];

        SourceParser()
        {
            float overbrightFactor = 1f;
            float gamma = 2.2f;
            for (int i = 0; i < 4096; i++)
            {
                // convert from linear 0..4 (x1024) to screen corrected vertex space (0..1?)
                float f = (float)Math.Pow(i / 1024.0, 1.0 / gamma);

                lineartovertex[i] = f * overbrightFactor;
                if (lineartovertex[i] > 1)
                    lineartovertex[i] = 1;

                //lineartolightmap[i] = f * 255 * overbrightFactor;
                //if (lineartolightmap[i] > 255)
                //    lineartolightmap[i] = 255;
            }
        }

        // linear (0..4) to screen corrected vertex space (0..1?)
        public float LinearToVertexLight(float f)
        {
            int i = (int)(f * 1024f);
            if (i > 4095)
                i = 4095;
            else if (i < 0)
                i = 0;

            return lineartovertex[i];
        }

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

            Header sHeader = new Header();
            sHeader.ident = id;
            sHeader.version = br.ReadInt32();

            // GoldSrc map
            if (sHeader.version == 30)
            {
                GoldSrcParser.LoadWorldMap(sHeader, br);
                return;
            }

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
            LoadWorldLights(br, sHeader);
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
            

            DispTreeLeafnum(world);
            SourceMap map = new SourceMap(world);
            map.Init();
            Renderer.Instance.SourceMap = map;
            long now = HighResolutionTimer.Ticks;
            for (int i = 0; i < world.sourceProps.Count; i++)
            {
                world.sourceProps[i].UpdateMesh();
            }
            float propLightingTime = (float)(HighResolutionTimer.Ticks - now)  / HighResolutionTimer.Frequency;

            Common.Instance.WriteLine("Finished prop lighting: {0:0.000}s", propLightingTime);
        }

        // Hook displacements into the bsp tree
        static void DispTreeLeafnum(World world)
        {
            //
            // get the number of displacements per leaf
            //
            List<dDispTri> tris = new List<dDispTri>();
            world.dispCollTrees = new DispCollTree[world.DispIndexToFaceIndex.Length];
            List<dDispVert> verts = new List<dDispVert>();
            int currTri = 0, currVert = 0;
            for (int i = 0; i < world.DispIndexToFaceIndex.Length; i++)
            {
                int j;
                int faceIndex = world.DispIndexToFaceIndex[i];
                int dispId = world.faces_t[faceIndex].dispinfo;
                ddispinfo_t info = world.ddispinfos[dispId];

                int power = info.power;
                int nVerts = (((1 << (power)) + 1) * ((1 << (power)) + 1));
                verts.Clear();
                // fail code
                for (j = 0; j < nVerts; j++)
                {
                    verts.Add(world.dispVerts[j + currVert]);
                }
                currVert += nVerts;

                int nTris = ((1 << (power)) * (1 << (power)) * 2);
                tris.Clear();
                for (j = 0; j < nTris; j++)
                {
                    tris.Add(world.dispTris[j + currTri]);
                }
                currTri += nTris;


                Displacement displace = new Displacement();
                displace.Surface = new DispSurface();
                DispSurface dispSurf = displace.Surface;
                dispSurf.m_PointStart = info.startPosition;
                dispSurf.m_Contents = info.contents;

                displace.InitDispInfo(info.power, info.minTess, info.smoothingAngle, verts, tris);

                dispSurf.m_Index = faceIndex;
                face_t face = world.faces_t[faceIndex];

                if (face.numedges > 4)
                    continue;

                Vector3[] surfPoints = new Vector3[4];
                dispSurf.m_PointCount = face.numedges;
                
                for (j = 0; j < face.numedges; j++)
                {
                    int eIndex = world.surfEdges[face.firstedge + j];
                    if (eIndex < 0)
                        surfPoints[j] = world.verts[world.edges[-eIndex].v[1]].position;
                    else
                        surfPoints[j] = world.verts[world.edges[eIndex].v[0]].position;
                }

                dispSurf.m_Points = surfPoints;
                dispSurf.FindSurfPointStartIndex();
                dispSurf.AdjustSurfPointData();

                //
                // generate the collision displacement surfaces
                //
                world.dispCollTrees[i] = new DispCollTree();
                DispCollTree dispTree = world.dispCollTrees[i];

                //
                // check for null faces, should have been taken care of in vbsp!!!
                //
                if (dispSurf.m_PointCount != 4)
                    continue;

                displace.Create();

                // new collision
                dispTree.Create(displace);

                
                DispTreeLeafnum_r(world, faceIndex, i, 0);
            }
        }

        static void DispTreeLeafnum_r(World world, int faceid, int collId, int nodeIndex)
        {                
            while (true)
            {
                //
                // leaf
                //
                if (nodeIndex < 0)
                {
                    //
                    // get leaf node
                    //
                    int leadIndex = -1 - nodeIndex;
                    dleaf_t pLeaf = world.leafs[leadIndex];

                    if (pLeaf.DisplacementIndexes != null)
                    {
                        KeyValuePair<int, int>[] indexes = pLeaf.DisplacementIndexes;
                        KeyValuePair<int, int>[] newid = new KeyValuePair<int, int>[indexes.Length + 1];
                        indexes.CopyTo(newid, 0);
                        newid[newid.Length - 1] = new KeyValuePair<int,int>(faceid, collId);
                        pLeaf.DisplacementIndexes = newid;
                    }
                    else
                    {
                        pLeaf.DisplacementIndexes = new KeyValuePair<int,int>[] { new KeyValuePair<int,int>(faceid, collId) };
                    }

                    return;
                }

                //
                // choose side(s) to traverse
                //
                dnode_t pNode =  world.nodes[nodeIndex];
                cplane_t pPlane = pNode.plane;

                // get box position relative to the plane
                Vector3 min = world.faces[faceid].BBox[0];
                Vector3 max = world.faces[faceid].BBox[1];
                int sideResult = Common.Instance.BoxOnPlaneSide(ref min, ref max, pPlane);

                // front side
                if (sideResult == 1)
                {
                    nodeIndex = pNode.children[0];
                }
                // back side
                else if (sideResult == 2)
                {
                    nodeIndex = pNode.children[1];
                }
                // split
                else
                {
                    DispTreeLeafnum_r(world, faceid, collId, pNode.children[0]);
                    nodeIndex = pNode.children[1];
                }

            }
        }

        static void LoadWorldLights(BinaryReader br, Header header)
        {
            br.BaseStream.Seek(header.lumps[15].fileofs, SeekOrigin.Begin);
            int numWorldlights = header.lumps[15].filelen / 88;
            if (header.lumps[15].filelen % 88 != 0)
                Common.Instance.WriteLine("LoadWorldLights: WARNING: Funny lump size");
            worldlight_t[] lights = new worldlight_t[numWorldlights];
            for (int i = 0; i < numWorldlights; i++)
            {
                worldlight_t light = new worldlight_t();
                light.Origin = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
                light.Intensity = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
                light.Normal = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());  // for surfaces and spotlights
                light.Cluster = br.ReadInt32();
                light.Type = (EmitType)br.ReadInt32();
                light.Style = br.ReadInt32();
                light.stopdot = br.ReadSingle();   // start of penumbra for emit_spotlight
                light.stopdot2 = br.ReadSingle();  // end of penumbra for emit_spotlight
                light.exponent = br.ReadSingle();
                light.radius = br.ReadSingle();    // cutoff distance
                // falloff for emit_spotlight + emit_point: 
                // 1 / (constant_attn + linear_attn * dist + quadratic_attn * dist^2)
                light.Constant_Attn = br.ReadSingle();
                light.Linear_Attn = br.ReadSingle();
                light.Quadratic_Attn = br.ReadSingle();
                light.Flags = br.ReadInt32();
                light.Texinfo = br.ReadInt32();
                light.Owner = br.ReadInt32();   // entity that this light it relative to

                // Fixup for backward compatability
                if (light.Type == EmitType.SPOTLIGHT)
                {
                    if (light.Quadratic_Attn == 0.0f && light.Linear_Attn == 0.0f && light.Constant_Attn == 0.0f)
                        light.Quadratic_Attn = 1.0f;
                    if (light.exponent == 0.0f)
                        light.exponent = 1.0f;
                }
                else if (light.Type == EmitType.POINT)
                {
                    // To match earlier lighting, use quadratic...
                    if (light.Quadratic_Attn == 0.0f && light.Linear_Attn == 0.0f && light.Constant_Attn == 0.0f)
                        light.Quadratic_Attn = 1.0f;
                }

                if (light.radius < 1)
                    light.radius = 0;

                lights[i] = light;
            }
            world.worldlights = lights;
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
                if (brushside.planenum == 9)
                {
                    int test = 2;
                }
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
            world.Entities = Entity.CreateEntities(world.EntityString);
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
                texinfo.lightmapVecs = new Vector3[2];
                texinfo.lightmapVecs2 = new float[2];

                // Read structure
                texinfo.textureVecs[0] = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                texinfo.textureVecs[1] = new Vector4(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                texinfo.lightmapVecs[0] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                texinfo.lightmapVecs2[0] = br.ReadSingle();
                texinfo.lightmapVecs[1] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                texinfo.lightmapVecs2[1] = br.ReadSingle();
                texinfo.flags = (SurfFlags)br.ReadInt32();
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
            br.BaseStream.Seek(header.lumps[10].fileofs, SeekOrigin.Begin);
            for (int i = 0; i < world.numLeafs; i++)
            {
                //
                dleaf_t leaf = new dleaf_t();
                leaf.contents = br.ReadInt32();
                leaf.cluster = br.ReadInt16();
                if (leaf.cluster > world.numClusters)
                    world.numClusters = leaf.cluster + 1;
                ushort packed = br.ReadUInt16();
                leaf.area = (short)((ushort)(packed << 7) >> 7);
                leaf.flags = (short)(packed >> 9);
                if (packed > 0)
                {
                    int test = 2;
                }
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
                else
                {
                    if (world.LightGrid != null && world.LightGrid.Length > i)
                        leaf.ambientLighting = world.LightGrid[i];
                }
                leaf.padding = br.ReadInt16();
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
                world.leafbrushes.Add((int)br.ReadUInt16());
            }
        }

        static void 
            LoadFaces(BinaryReader br, Header header)
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
            world.edges = edges;
            // Read surfedges
            br.BaseStream.Seek(header.lumps[13].fileofs, SeekOrigin.Begin);
            int nSurfEdges = header.lumps[13].filelen / sizeof(int);
            int[] surfEdges = new int[nSurfEdges];
            for (int i = 0; i < nSurfEdges; i++)
            {
                surfEdges[i] = br.ReadInt32();
            }
            world.surfEdges = surfEdges;

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

                // Reverse mapping from displacement to face
                if (face.dispinfo != -1 && face.dispinfo < world.ddispinfos.Length)
                {
                    world.DispIndexToFaceIndex[face.dispinfo] = i;
                }
            }

            // Prepare lightmap texture
            Texture lightmapTexture = new Texture(Renderer.Instance.device, LightmapSize.Width, LightmapSize.Height, 1, Usage.Dynamic, Format.A16B16G16R16F, (Renderer.Instance.Is3D9Ex ? Pool.Default : Pool.Managed));
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
                        //Vector3 vecs = new Vector3(face.texinfo.lightmapVecs[0].X, face.texinfo.lightmapVecs[0].Y, face.texinfo.lightmapVecs[0].Z);
                        //Vector3 vect = new Vector3(face.texinfo.lightmapVecs[1].X, face.texinfo.lightmapVecs[1].Y, face.texinfo.lightmapVecs[1].Z);

                        l_s = Vector3.Dot(vert, face.texinfo.lightmapVecs[0]) +
                            face.texinfo.lightmapVecs2[0] - face.face_t.LightmapTextureMinsInLuxels[0];
                        l_s /= face.face_t.LightmapTextureSizeInLuxels[0];

                        l_t = Vector3.Dot(vert, face.texinfo.lightmapVecs[1]) +
                            face.texinfo.lightmapVecs2[1] - face.face_t.LightmapTextureMinsInLuxels[1];
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
            }

            //DispCollTree[] dispCollTrees = new DispCollTree[world.ddispinfos.Length];
            //for (int i = 0; i < dispCollTrees.Length; i++)
            //{
            //    dispCollTrees[i] = new DispCollTree();
            //}

            // Handle displacement face
            int iCurVert = 0, iCurTri = 0;
            for (int i = 0; i < world.ddispinfos.Length; i++)
            {
                int nFaceIndex = world.DispIndexToFaceIndex[i];

                // Check for missing mapping to face
                if (nFaceIndex == 0)
                    continue;
                
                // Get the displacement info
                ddispinfo_t currentDispInfo = world.ddispinfos[i];
                Face face = world.faces[nFaceIndex];

                //// Read in vertices
                //int nVerts = (((1 << (currentDispInfo.power)) + 1) * ((1 << (currentDispInfo.power)) + 1));
                //List<dDispVert> dispVerts = new List<dDispVert>();
                //for (int j = 0; j < nVerts; j++)
                //{
                //    dispVerts.Add(world.dispVerts[iCurVert + j]);
                //}
                //iCurVert += nVerts;

                //// Read in triangles
                //int nTris = ((1 << (currentDispInfo.power)) * (1 << (currentDispInfo.power)) * 2);
                //List<dDispTri> dispTris = new List<dDispTri>();
                //for (int j = 0; j < nTris; j++)
                //{
                //    dispTris.Add(world.dispTris[iCurTri + j]);
                //}
                //iCurTri += nTris;

                //Displacement disp = new Displacement();
                //DispSurface dispSurf = disp.Surface;
                //dispSurf.m_PointStart = currentDispInfo.startPosition;
                //dispSurf.m_Contents = currentDispInfo.contents;

                //disp.InitDispInfo(currentDispInfo.power, currentDispInfo.minTess, currentDispInfo.smoothingAngle, dispVerts, dispTris);

                //// Hook the disp surface to the face
                //dispSurf.m_Index = nFaceIndex;

                //// get points
                //if (world.faces_t[nFaceIndex].numedges > 4)
                //    continue;

                //face_t fac = world.faces_t[nFaceIndex];

                //Vector3[] surfPoints = new Vector3[4];
                //dispSurf.m_PointCount = fac.numedges;
                //int h = 0;
                //for (h = 0; h < fac.numedges; h++ )
                //{
                //    int eIndex = surfEdges[fac.firstedge + h];
                //    if (eIndex < 0)
                //    {
                //        surfPoints[h] = world.verts[edges[-eIndex].v[1]].position;
                //    }
                //    else
                //    {
                //        surfPoints[h] = world.verts[edges[eIndex].v[0]].position;
                //    }
                //}

                //for (h = 0; h < 4; h++)
                //{
                //    dispSurf.m_Points[h] = surfPoints[h];
                //}

                //dispSurf.FindSurfPointStartIndex();
                //dispSurf.AdjustSurfPointData();

                ////
                //// generate the collision displacement surfaces
                ////
                //DispCollTree dispTree = dispCollTrees[i];
                //dispTree.Power = 0;

                ////
                //// check for null faces, should have been taken care of in vbsp!!!
                ////
                //int pointCount = dispSurf.m_PointCount;
                //if (pointCount != 4)
                //    continue;

                //disp.Create();

                //DispCollTree pDispTree = dispCollTrees[i];
                //pDispTree.Power = 0;
                //pDispTree.Create(disp);

                // Generate the displacement surface
                createDispSurface(face, currentDispInfo, world, nFaceIndex);
            }

            //world.dispCollTrees = dispCollTrees;
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

        static void createDispSurface(Face face, ddispinfo_t dispInfo, World map, int faceIndex)
        {
            face.HasDisplacement = true;
            Face actualFace = face;
            int ndx = faceIndex;
            while (actualFace.face_t.origFace > 0 && actualFace.face_t.origFace != ndx)
            {
                ndx = actualFace.face_t.origFace;
                actualFace = world.faces_t[actualFace.face_t.origFace].face;
            }
            actualFace.HasDisplacement = true;
            actualFace.DisplaceFaces = new int[] { faceIndex };

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

            Vector3[] BBox = new Vector3[2];
            BBox[0] = Vector3.Zero;
            BBox[1] = Vector3.Zero;
            // Build real vertices && BBox
            for (int i = 0; i < verts.Count; i++)
            {
                BBox[0] = Vector3.Minimize(verts[i], BBox[0]);
                BBox[1] = Vector3.Maximize(verts[i], BBox[1]);
                VertexPositionNormalTexturedLightmap vert = new VertexPositionNormalTexturedLightmap(verts[i], normals[i], texcoords[i], lightcoords[i]);
                world.verts.Add(vert);
            }

            face.BBox = BBox;

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
            world.DispIndexToFaceIndex = new int[nDispInfo];
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

            // Read DispTris
            br.BaseStream.Seek(header.lumps[48].fileofs, SeekOrigin.Begin);
            int nDispTris = header.lumps[48].filelen / 2;
            dDispTri[] dispTris = new dDispTri[nDispTris];
            for (int i = 0; i < nDispTris; i++)
            {
                dDispTri vert = new dDispTri();
                vert.Tags = br.ReadUInt16();
                dispTris[i] = vert;
            }
            world.dispTris = dispTris;
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

        void UpdateStaticPropLighting(SourceProp prop)
        {
            List<Color3> colors = new List<Color3>();
            List<Vector4> lightmem = new List<Vector4>();
            Matrix matrix = Matrix.AffineTransformation(1.0f, Vector3.Zero, Quaternion.RotationYawPitchRoll(prop.prop_t.Angles.X, -prop.prop_t.Angles.Z, prop.prop_t.Angles.Y), prop.prop_t.Origin);
            for (int bodyID = 0; bodyID < prop.srcModel.BodyParts.Count; bodyID++)
            {
                BodyPart bpart = prop.srcModel.BodyParts[bodyID];

                for (int modelid = 0; modelid < bpart.VTXBodyPart.Models.Length; modelid++)
                {
                    VTXModel mdl = bpart.VTXBodyPart.Models[modelid];

                    ComputeModelVertexLighting(prop, mdl, matrix, lightmem, colors);

                    //for (int meshid = 0; meshid < mdl.Meshes.Count; meshid++)
                    //{
                    //    MDLMesh mesh = mdl.Meshes[meshid];

                    //    // Add specular?
                    //}
                }
            }
        }

        void ComputeModelVertexLighting(SourceProp prop, VTXModel mdl, Matrix matrix, List<Vector4> lightmem, List<Color3> colors)
        {
            // for each vertex
            //for (int i = 0; i < mdl.Lods[0].Meshes[0].StripGroups; ++i)
            {
                //ComputeVertexLightFromSpericalSamples
                //Vector3 pos = mdl.Meshes[0].
            }
        }

        //-----------------------------------------------------------------------------
        // Computes the static vertex lighting term from a large number of spherical samples
        //-----------------------------------------------------------------------------
        public bool ComputeVertexLightFromSpericalSamples(Vector3 pos, Vector3 normal, SourceProp prop, out Vector3 tempmem)
        {
            tempmem = Vector3.Zero;
            // Check to see if this vertex is in solid
            trace_t trace = ClipMap.Instance.Box_Trace(pos, pos, Vector3.Zero, Vector3.Zero, 0, 1);
            if (trace.allsolid || trace.startsolid)
                return false;
            int i;
            float t = 0.0f;
            // find any ambient lights
            worldlight_t skyLight = FindAmbientLight();

            // sample world by casting N rays distributed across a sphere
            //float t = 0.0f;
            Vector3 upend = Vector3.Zero, color = Vector3.Zero;
            //int i;
            //for (i = 0; i < 2; i++) // 162
            //{
            //    float flDot = Vector3.Dot(normal, s_randdir[i]);
            //    if (flDot < 0.0f)
            //        continue;

            //    // FIXME: a good optimization would be to scale this per leaf
            //    upend = ViewParams.VectorMA(pos, 56891, s_randdir[i]);

            //    // Now that we've got a ray, see what surface we've hit
            //    int surfid = R_LightVec(pos, upend, false, ref color);
            //    if (surfid == -1)
            //        continue;

            //    // FIXME: Maybe make sure we aren't obstructed by static props?
            //    // To do this, R_LightVec would need to return distance of hit...
            //    // Or, we need another arg to R_LightVec to return black when hitting a static prop
            //    ComputeAmbientFromSurface(surfid, skyLight, ref color);

            //    t += flDot;
            //    tempmem = ViewParams.VectorMA(tempmem, flDot, color);
            //}

            //if (t != 0.0f)
            //    tempmem /= t;

            // Now deal with direct lighting
            bool hasSkylight = false;

            // Figure out the PVS info for this location
            int leaf = ClipMap.Instance.PointLeafnum(pos);
            bool[] pvs = ClipMap.Instance.ClusterPVS(ClipMap.Instance.LeafCluster(leaf));
            Vector3 vecdirection = Vector3.Zero;
            // Now add in the direct lighting
            for (i = 0; i < world.worldlights.Length; i++)
            {
                worldlight_t wl = world.worldlights[i];

                // FIXME: This is sort of a hack; only one skylight is allowed in the
                // lighting...
                if (wl.Type == EmitType.SKYLIGHT && hasSkylight)
                    continue;

                if (wl.Cluster < 0 || pvs == null || !pvs[wl.Cluster])
                    continue;

                float flRatio = LightIntensityAndDirectionAtpoint(wl, pos, 4, ref vecdirection);

                // No light contribution? Get outta here!
                if (flRatio <= 0.0f)
                    continue;

                // Check if we've got a skylight
                if (wl.Type == EmitType.SKYLIGHT)
                    hasSkylight = true;

                // Figure out spotlight attenuation
                float flAngularRatio = WorldLightAngle(wl, wl.Normal, normal, vecdirection);

                // Add in the direct lighting
                tempmem = ViewParams.VectorMA(tempmem, flAngularRatio * flRatio, wl.Intensity);
            }

            return true;
        }

        float WorldLightAngle(worldlight_t wl, Vector3 lnormal, Vector3 snormal, Vector3 delta)
        {
            float dot = 0, dot2 = 0, ratio = 0;
            switch (wl.Type)
            {
                case EmitType.SURFACE:
                    dot = Vector3.Dot(snormal, delta);
                    if (dot < 0)
                        return 0;

                    dot2 = -Vector3.Dot(delta, lnormal);
                    if (dot2 <= 0.1f / 10)
                        return 0;   // behind light surface

                    return dot * dot2;
                case EmitType.POINT:
                    dot = Vector3.Dot(snormal, delta);
                    if (dot < 0)
                        return 0;
                    return dot;
                case EmitType.SPOTLIGHT:
                    dot = Vector3.Dot(snormal, delta);
                    if (dot < 0)
                        return 0;

                    dot2 = -Vector3.Dot(delta, lnormal);
                    if (dot2 <= wl.stopdot2)
                        return 0;   // outside light cone

                    ratio = dot;
                    if (dot2 >= wl.stopdot)
                        return ratio;   // inside inner cone

                    if ((wl.exponent == 1 || wl.exponent == 0))
                    {
                        ratio *= (dot2 - wl.stopdot2) / (wl.stopdot - wl.stopdot2);
                    }
                    else
                    {
                        ratio *= (float)Math.Pow((dot2 - wl.stopdot2) / (wl.stopdot - wl.stopdot2), wl.exponent);
                    }
                    return ratio;
                case EmitType.SKYLIGHT:
                    dot2 = -Vector3.Dot(snormal, lnormal);
                    if (dot2 < 0)
                        return 0;
                    return dot2;
                case EmitType.QUAKELIGHT:
                    // linear falloff
                    dot = Vector3.Dot(snormal, delta);
                    if (dot < 0)
                        return 0;
                    return dot;
                case EmitType.SKYAMBIENT:
                    // not supported
                    return 1;
            }
            return 0;
        }

        //-----------------------------------------------------------------------------
        // This method returns the effective intensity of a light as seen from
        // a particular point. PVS is used to speed up the task.
        //-----------------------------------------------------------------------------
        float LightIntensityAndDirectionAtpoint(worldlight_t light, Vector3 mid, int flags, ref Vector3 direction)
        {
            // Special case lights
            switch (light.Type)
            {
                case EmitType.SKYLIGHT:
                    // There can be more than one skylight, but we should only
                    // ever be affected by one of them (multiple ones are created from
                    // a single light in vrad)

                    // check to see if you can hit the sky texture
                    Vector3 end = ViewParams.VectorMA(mid, -65500, light.Normal);
                    trace_t trace = ClipMap.Instance.Box_Trace(mid, end, Vector3.Zero, Vector3.Zero, 0, (int)(brushflags.CONTENTS_SOLID | brushflags.CONTENTS_MOVEABLE | brushflags.CONTENTS_SLIME | brushflags.CONTENTS_OPAQUE));

                    // Here, we didn't hit the sky, so we must be in shadow
                    if (((SurfFlags)trace.surfaceFlags & SurfFlags.SURF_SKY) != SurfFlags.SURF_SKY)
                        return 0.0f;

                    // fudge delta and dist for skylights
                    direction.X = direction.Y = direction.Z = 0;
                    return 1.0f;
                case EmitType.SKYAMBIENT:
                    // always ignore these
                    return 0.0f;
            }
            // all other lights
            
            // check distance
            direction = light.Origin - mid;
            float ratio = WorldLightDistanceFalloff(light, direction, (flags & 2) != 0);

            // Add in light style component
            //ratio *= light.Style;

            // Early out for really low-intensity lights
            // That way we don't need to ray-cast or normalize
            float intensity = Math.Max(light.Intensity[0], light.Intensity[1]);
            intensity = Math.Max(intensity, light.Intensity[2]);

            // This is about 1/256
            if (intensity * ratio < 1f / 256f)
                return 0.0f;


            float dist = direction.Length();
            direction.Normalize();

            if ((flags & 1) == 1) // LIGHT_NO_OCCLUSION_CHECK
                return ratio;

            trace_t pm = ClipMap.Instance.Box_Trace(mid, light.Origin, Vector3.Zero, Vector3.Zero, 0, 0x1 | 0x40);

            // hack
            if ((1f - pm.fraction) * dist > 8)
                return 0;

            return ratio;
        }

        float WorldLightDistanceFalloff(worldlight_t light, Vector3 delta, bool noRadiusCheck)
        {
            float falloff = 0.0f;
            switch (light.Type)
            {
                case EmitType.SURFACE:
                    // Cull out stuff that's too far
                    if (light.radius != 0)
                    {
                        if (Vector3.Dot(delta, delta) > (light.radius * light.radius))
                            return 0.0f;
                    }

                    return InvRSquared(delta);
                case EmitType.SKYLIGHT:
                    return 1.0f;
                case EmitType.QUAKELIGHT:
                    // X - r;
                    falloff = light.Linear_Attn - (float)Math.Sqrt(Vector3.Dot(delta, delta));
                    if (falloff < 0f)
                        return 0f;

                    return falloff;
                case EmitType.SKYAMBIENT:
                    return 1.0f;
                case EmitType.SPOTLIGHT:
                case EmitType.POINT:    // directional & positional
                    float dist2 = Vector3.Dot(delta, delta);
                    float dist = (float)Math.Sqrt(dist2);

                    // Cull out stuff that's too far
                    if (!noRadiusCheck && (light.radius != 0) && (dist > light.radius))
                        return 0f;

                    return 1f / (light.Constant_Attn + light.Linear_Attn * dist + light.Quadratic_Attn * dist2);
            }
            return 1f;
        }

        float InvRSquared(Vector3 v)
        {
            float r2 = Vector3.Dot(v, v);
            return r2 < 1.0f ? 1.0f : 1f / r2;
        }

        public class LightVecState
        {
            public Vector3 start;
            public Vector3 end;
            public float hitFrac;
            public int skySurf;
            public bool useLightstyles;
        }

        void ComputeAmbientFromSurface(int surfid, worldlight_t skyLight,ref Vector3 color)
        {
            if (surfid != -1)
            {
                // If we hit the sky, use the sky ambient
                if ((world.faces_t[surfid].face.texinfo.flags & SurfFlags.SURF_SKY) == SurfFlags.SURF_SKY)
                {
                    if (skyLight != null)   // add in sky ambient
                        color = skyLight.Intensity;
                }
                else
                {
                    Vector3 reflectivity = world.faces_t[surfid].face.texinfo.texdata_t.reflectivity;
                    color.X *= reflectivity.X;
                    color.Y *= reflectivity.Y;
                    color.Z *= reflectivity.Z;
                }
            }
        }

        int R_LightVec(Vector3 start, Vector3 end, bool useLightStyles, ref Vector3 c)
        {
            c.X = c.Y = c.Z = 0;
            int skysurf = -1;
            LightVecState state = new LightVecState();
            state.skySurf = -1;
            state.start = start;
            state.end = end;
            state.hitFrac = 1.0f;
            int retSurfId = RecursiveLightPoint(0, 0f, 1f, ref c, state);

            if (retSurfId == -1 && skysurf != -1)
                return skysurf;

            return retSurfId;
        }

        int FindIntersectionSurfaceAtLeaf(dleaf_t leaf, float start, float end, ref Vector3 c, LightVecState state)
        {
            int clostestSurf = -1;
            for (int i = 0; i < leaf.numleaffaces; i++)
            {
                int surfid = world.leafFaces[leaf.firstleafface + i];
                face_t face = world.faces_t[surfid];

                // Don't add surfaces that have displacement; they are handled above
                // In fact, don't even set the vis frame; we need it unset for translucent
                // displacement code
                if (face.dispinfo != -1)
                    continue;

                if ((face.face.texinfo.flags & SurfFlags.SURF_NODRAW) == SurfFlags.SURF_NODRAW)
                    continue;

                cplane_t plane = face.face.plane_t;

                // Backface cull...
                if (Vector3.Dot(plane.normal, (state.end - state.start)) > 0.0f)
                    continue;

                float startdotn = Vector3.Dot(state.start, plane.normal);
                float deltadotn = Vector3.Dot((state.end - state.start), plane.normal);

                float front = startdotn + start * deltadotn - plane.dist;
                float back = startdotn + end * deltadotn - plane.dist;

                bool side = front < 0.0f;

                // Blow it off if it doesn't split the plane...
                if ((back < 0.0f) == side)
                    continue;

                // Don't test a surface that is farther away from the closest found intersection
                float frac = front / (front - back);
                if (frac >= state.hitFrac)
                    continue;

                float mid = start * (1.0f - frac) + end * frac;

                // Check this surface to see if there's an intersection
                if (FindIntersectionAtSurface(surfid, mid, ref c, state))
                {
                    clostestSurf = surfid;
                }
            }

            // Return the closest surface hit
            return clostestSurf;
        }

        int RecursiveLightPoint(int n, float start, float end, ref Vector3 c, LightVecState state)
        {
            while (n >= 0)
            {
                dnode_t node = world.nodes[n];
                cplane_t plane = node.plane;

                float startDotn = Vector3.Dot(state.start, plane.normal);
                float deltaDotn = Vector3.Dot(state.end - state.start, plane.normal);

                float front = startDotn + start * deltaDotn - plane.dist;
                float back = deltaDotn + end * deltaDotn - plane.dist;
                bool side = front < 0;

                // If they're both on the same side of the plane, don't bother to split
                // just check the appropriate child
                int surfid = 0;
                if ((back < 0) == side)
                {
                    n = node.children[side ? 1 : 0];
                    continue;
                    //surfid = RecursiveLightPoint(node.children[side ? 1 : 0], start, end, ref c, state);
                    //return surfid;
                }

                // calculate mid point
                float frac = front / (front - back);
                float mid = start * (1.0f - frac) + end * frac;

                // go down front side	
                surfid = RecursiveLightPoint(node.children[side ? 1 : 0], start, mid, ref c, state);
                if (surfid != -1)
                    return surfid;  // hit something

                // check for impact on this node
                surfid = FindIntersectionSurfaceAtNode(n, mid, ref c, state);
                if (surfid != -1)
                    return surfid;

                // go down back side
                surfid = RecursiveLightPoint(node.children[!side ? 1 : 0], mid, end, ref c, state);
                return surfid;

                break;
            }

            // didn't hit anything
            if (n < 0)
            {
                // FIXME: Should we always do this? It could get expensive...
                // Check all the faces at the leaves
                return FindIntersectionSurfaceAtLeaf(world.leafs[-1 - n], start, end, ref c, state);
            }

            return -1;
        }

        int FindIntersectionSurfaceAtNode(int n, float t, ref Vector3 c, LightVecState state)
        {
            dnode_t node = world.nodes[n];
            int surfid = node.firstface;
            for (int i = 0; i < node.numfaces; i++, surfid++)
            {
                // Don't immediately return when we hit sky; 
                // we may actually hit another surface
                SurfFlags flags = world.faces[world.leafFaces[surfid]].texinfo.flags;
                if ((flags & SurfFlags.SURF_SKY) == SurfFlags.SURF_SKY)
                {
                    state.skySurf = surfid;
                    continue;
                }

                // Don't let water surfaces affect us
                if ((flags & SurfFlags.SURF_WARP) == SurfFlags.SURF_WARP)
                    continue;

                if(FindIntersectionAtSurface(surfid, t, ref c, state))
                    return surfid;
            }
            return -1;
        }

        //-----------------------------------------------------------------------------
        // Tests a particular surface
        //-----------------------------------------------------------------------------
        bool FindIntersectionAtSurface(int surfid, float f, ref Vector3 c, LightVecState state)
        {
            // no lightmaps on this surface? punt...
            // FIXME: should be water surface?
            SurfFlags flags = world.faces[world.leafFaces[surfid]].texinfo.flags;
            if ((flags & SurfFlags.SURF_NOLIGHT) == SurfFlags.SURF_NOLIGHT) // SURFDRAW_NOLIGHT
                return false;

            // Compute the actual point
            Vector3 pt = ViewParams.VectorMA(state.start, f, state.end - state.start);
            texinfo_t tex = world.faces[world.leafFaces[surfid]].texinfo;

            // See where in lightmap space our intersection point is 

            float s = Vector3.Dot(pt, tex.lightmapVecs[0]) +
                tex.lightmapVecs2[0];
            float t = Vector3.Dot(pt, tex.lightmapVecs[1]) +
                tex.lightmapVecs2[1];

            // Not in the bounds of our lightmap? punt...
            int[] luxMins = world.faces[world.leafFaces[surfid]].face_t.LightmapTextureMinsInLuxels;
            if (s < luxMins[0] ||
                t < luxMins[1])
                return false;

            // assuming a square lightmap (FIXME: which ain't always the case),
            // lets see if it lies in that rectangle. If not, punt...
            float ds = s - luxMins[0];
            float dt = t - luxMins[1];
            int[] luxExtends = world.faces[world.leafFaces[surfid]].face_t.LightmapTextureSizeInLuxels;
            if (ds > luxExtends[0] ||
                dt > luxExtends[1])
                return false;

            // Store off the hit distance...
            state.hitFrac = f;

            // You heard the man!
            // TODO

            ComputeLightmapColorFromAverage(world.faces[world.leafFaces[surfid]].face_t, state.useLightstyles, ref c);

            return true;
        }


        //-----------------------------------------------------------------------------
        // Computes the lightmap color at a particular point
        //-----------------------------------------------------------------------------
        void ComputeLightmapColorFromAverage(face_t face, bool useLightStyle, ref Vector3 c)
        {
            int nMaxMaps = useLightStyle ? 4 : 1;
            for (int maps = 0; maps < nMaxMaps && face.styles[maps] != 255; maps++)
            {
                float scale = 16f * ((float)face.styles[maps] / 264.0f);
                Color3 color = world.LightData[face.lightlumpofs - (maps + 1)];
                c.X += color.Red * scale;
                c.Y += color.Green * scale;
                c.Z += color.Blue * scale;
            }
        }
        
        worldlight_t FindAmbientLight()
        {
            for (int i = 0; i < world.worldlights.Length; i++)
            {
                if (world.worldlights[i].Type == EmitType.SKYAMBIENT)
                    return world.worldlights[i];
            }
            return null;
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

        public Vector3[] s_randdir = new Vector3[] 
        {
            new Vector3(-0.525731f, 0.000000f, 0.850651f), 
            new Vector3(-0.442863f, 0.238856f, 0.864188f), 
            new Vector3(-0.295242f, 0.000000f, 0.955423f), 
            new Vector3(-0.309017f, 0.500000f, 0.809017f), 
            new Vector3(-0.162460f, 0.262866f, 0.951056f), 
            new Vector3(0.000000f, 0.000000f, 1.000000f), 
            new Vector3(0.000000f, 0.850651f, 0.525731f), 
            new Vector3(-0.147621f, 0.716567f, 0.681718f), 
            new Vector3(0.147621f, 0.716567f, 0.681718f), 
            new Vector3(0.000000f, 0.525731f, 0.850651f), 
            new Vector3(0.309017f, 0.500000f, 0.809017f), 
            new Vector3(0.525731f, 0.000000f, 0.850651f), 
            new Vector3(0.295242f, 0.000000f, 0.955423f), 
            new Vector3(0.442863f, 0.238856f, 0.864188f), 
            new Vector3(0.162460f, 0.262866f, 0.951056f), 
            new Vector3(-0.681718f, 0.147621f, 0.716567f), 
            new Vector3(-0.809017f, 0.309017f, 0.500000f), 
            new Vector3(-0.587785f, 0.425325f, 0.688191f), 
            new Vector3(-0.850651f, 0.525731f, 0.000000f), 
            new Vector3(-0.864188f, 0.442863f, 0.238856f), 
            new Vector3(-0.716567f, 0.681718f, 0.147621f), 
            new Vector3(-0.688191f, 0.587785f, 0.425325f), 
            new Vector3(-0.500000f, 0.809017f, 0.309017f), 
            new Vector3(-0.238856f, 0.864188f, 0.442863f), 
            new Vector3(-0.425325f, 0.688191f, 0.587785f), 
            new Vector3(-0.716567f, 0.681718f, -0.147621f), 
            new Vector3(-0.500000f, 0.809017f, -0.309017f), 
            new Vector3(-0.525731f, 0.850651f, 0.000000f), 
            new Vector3(0.000000f, 0.850651f, -0.525731f), 
            new Vector3(-0.238856f, 0.864188f, -0.442863f), 
            new Vector3(0.000000f, 0.955423f, -0.295242f), 
            new Vector3(-0.262866f, 0.951056f, -0.162460f), 
            new Vector3(0.000000f, 1.000000f, 0.000000f), 
            new Vector3(0.000000f, 0.955423f, 0.295242f), 
            new Vector3(-0.262866f, 0.951056f, 0.162460f), 
            new Vector3(0.238856f, 0.864188f, 0.442863f), 
            new Vector3(0.262866f, 0.951056f, 0.162460f), 
            new Vector3(0.500000f, 0.809017f, 0.309017f), 
            new Vector3(0.238856f, 0.864188f, -0.442863f), 
            new Vector3(0.262866f, 0.951056f, -0.162460f), 
            new Vector3(0.500000f, 0.809017f, -0.309017f), 
            new Vector3(0.850651f, 0.525731f, 0.000000f), 
            new Vector3(0.716567f, 0.681718f, 0.147621f), 
            new Vector3(0.716567f, 0.681718f, -0.147621f), 
            new Vector3(0.525731f, 0.850651f, 0.000000f), 
            new Vector3(0.425325f, 0.688191f, 0.587785f), 
            new Vector3(0.864188f, 0.442863f, 0.238856f), 
            new Vector3(0.688191f, 0.587785f, 0.425325f), 
            new Vector3(0.809017f, 0.309017f, 0.500000f), 
            new Vector3(0.681718f, 0.147621f, 0.716567f), 
            new Vector3(0.587785f, 0.425325f, 0.688191f), 
            new Vector3(0.955423f, 0.295242f, 0.000000f), 
            new Vector3(1.000000f, 0.000000f, 0.000000f), 
            new Vector3(0.951056f, 0.162460f, 0.262866f), 
            new Vector3(0.850651f, -0.525731f, 0.000000f), 
            new Vector3(0.955423f, -0.295242f, 0.000000f), 
            new Vector3(0.864188f, -0.442863f, 0.238856f), 
            new Vector3(0.951056f, -0.162460f, 0.262866f), 
            new Vector3(0.809017f, -0.309017f, 0.500000f), 
            new Vector3(0.681718f, -0.147621f, 0.716567f), 
            new Vector3(0.850651f, 0.000000f, 0.525731f), 
            new Vector3(0.864188f, 0.442863f, -0.238856f), 
            new Vector3(0.809017f, 0.309017f, -0.500000f), 
            new Vector3(0.951056f, 0.162460f, -0.262866f), 
            new Vector3(0.525731f, 0.000000f, -0.850651f), 
            new Vector3(0.681718f, 0.147621f, -0.716567f), 
            new Vector3(0.681718f, -0.147621f, -0.716567f), 
            new Vector3(0.850651f, 0.000000f, -0.525731f), 
            new Vector3(0.809017f, -0.309017f, -0.500000f), 
            new Vector3(0.864188f, -0.442863f, -0.238856f), 
            new Vector3(0.951056f, -0.162460f, -0.262866f), 
            new Vector3(0.147621f, 0.716567f, -0.681718f), 
            new Vector3(0.309017f, 0.500000f, -0.809017f), 
            new Vector3(0.425325f, 0.688191f, -0.587785f), 
            new Vector3(0.442863f, 0.238856f, -0.864188f), 
            new Vector3(0.587785f, 0.425325f, -0.688191f), 
            new Vector3(0.688191f, 0.587785f, -0.425325f), 
            new Vector3(-0.147621f, 0.716567f, -0.681718f), 
            new Vector3(-0.309017f, 0.500000f, -0.809017f), 
            new Vector3(0.000000f, 0.525731f, -0.850651f), 
            new Vector3(-0.525731f, 0.000000f, -0.850651f), 
            new Vector3(-0.442863f, 0.238856f, -0.864188f), 
            new Vector3(-0.295242f, 0.000000f, -0.955423f), 
            new Vector3(-0.162460f, 0.262866f, -0.951056f), 
            new Vector3(0.000000f, 0.000000f, -1.000000f), 
            new Vector3(0.295242f, 0.000000f, -0.955423f), 
            new Vector3(0.162460f, 0.262866f, -0.951056f), 
            new Vector3(-0.442863f, -0.238856f, -0.864188f), 
            new Vector3(-0.309017f, -0.500000f, -0.809017f), 
            new Vector3(-0.162460f, -0.262866f, -0.951056f), 
            new Vector3(0.000000f, -0.850651f, -0.525731f), 
            new Vector3(-0.147621f, -0.716567f, -0.681718f), 
            new Vector3(0.147621f, -0.716567f, -0.681718f), 
            new Vector3(0.000000f, -0.525731f, -0.850651f), 
            new Vector3(0.309017f, -0.500000f, -0.809017f), 
            new Vector3(0.442863f, -0.238856f, -0.864188f), 
            new Vector3(0.162460f, -0.262866f, -0.951056f), 
            new Vector3(0.238856f, -0.864188f, -0.442863f), 
            new Vector3(0.500000f, -0.809017f, -0.309017f), 
            new Vector3(0.425325f, -0.688191f, -0.587785f), 
            new Vector3(0.716567f, -0.681718f, -0.147621f), 
            new Vector3(0.688191f, -0.587785f, -0.425325f), 
            new Vector3(0.587785f, -0.425325f, -0.688191f), 
            new Vector3(0.000000f, -0.955423f, -0.295242f), 
            new Vector3(0.000000f, -1.000000f, 0.000000f), 
            new Vector3(0.262866f, -0.951056f, -0.162460f), 
            new Vector3(0.000000f, -0.850651f, 0.525731f), 
            new Vector3(0.000000f, -0.955423f, 0.295242f), 
            new Vector3(0.238856f, -0.864188f, 0.442863f), 
            new Vector3(0.262866f, -0.951056f, 0.162460f), 
            new Vector3(0.500000f, -0.809017f, 0.309017f), 
            new Vector3(0.716567f, -0.681718f, 0.147621f), 
            new Vector3(0.525731f, -0.850651f, 0.000000f), 
            new Vector3(-0.238856f, -0.864188f, -0.442863f), 
            new Vector3(-0.500000f, -0.809017f, -0.309017f), 
            new Vector3(-0.262866f, -0.951056f, -0.162460f), 
            new Vector3(-0.850651f, -0.525731f, 0.000000f), 
            new Vector3(-0.716567f, -0.681718f, -0.147621f), 
            new Vector3(-0.716567f, -0.681718f, 0.147621f), 
            new Vector3(-0.525731f, -0.850651f, 0.000000f), 
            new Vector3(-0.500000f, -0.809017f, 0.309017f), 
            new Vector3(-0.238856f, -0.864188f, 0.442863f), 
            new Vector3(-0.262866f, -0.951056f, 0.162460f), 
            new Vector3(-0.864188f, -0.442863f, 0.238856f), 
            new Vector3(-0.809017f, -0.309017f, 0.500000f), 
            new Vector3(-0.688191f, -0.587785f, 0.425325f), 
            new Vector3(-0.681718f, -0.147621f, 0.716567f), 
            new Vector3(-0.442863f, -0.238856f, 0.864188f), 
            new Vector3(-0.587785f, -0.425325f, 0.688191f), 
            new Vector3(-0.309017f, -0.500000f, 0.809017f), 
            new Vector3(-0.147621f, -0.716567f, 0.681718f), 
            new Vector3(-0.425325f, -0.688191f, 0.587785f), 
            new Vector3(-0.162460f, -0.262866f, 0.951056f), 
            new Vector3(0.442863f, -0.238856f, 0.864188f), 
            new Vector3(0.162460f, -0.262866f, 0.951056f), 
            new Vector3(0.309017f, -0.500000f, 0.809017f), 
            new Vector3(0.147621f, -0.716567f, 0.681718f), 
            new Vector3(0.000000f, -0.525731f, 0.850651f), 
            new Vector3(0.425325f, -0.688191f, 0.587785f), 
            new Vector3(0.587785f, -0.425325f, 0.688191f), 
            new Vector3(0.688191f, -0.587785f, 0.425325f), 
            new Vector3(-0.955423f, 0.295242f, 0.000000f), 
            new Vector3(-0.951056f, 0.162460f, 0.262866f), 
            new Vector3(-1.000000f, 0.000000f, 0.000000f), 
            new Vector3(-0.850651f, 0.000000f, 0.525731f), 
            new Vector3(-0.955423f, -0.295242f, 0.000000f), 
            new Vector3(-0.951056f, -0.162460f, 0.262866f), 
            new Vector3(-0.864188f, 0.442863f, -0.238856f), 
            new Vector3(-0.951056f, 0.162460f, -0.262866f), 
            new Vector3(-0.809017f, 0.309017f, -0.500000f), 
            new Vector3(-0.864188f, -0.442863f, -0.238856f), 
            new Vector3(-0.951056f, -0.162460f, -0.262866f), 
            new Vector3(-0.809017f, -0.309017f, -0.500000f), 
            new Vector3(-0.681718f, 0.147621f, -0.716567f), 
            new Vector3(-0.681718f, -0.147621f, -0.716567f), 
            new Vector3(-0.850651f, 0.000000f, -0.525731f), 
            new Vector3(-0.688191f, 0.587785f, -0.425325f), 
            new Vector3(-0.587785f, 0.425325f, -0.688191f), 
            new Vector3(-0.425325f, 0.688191f, -0.587785f), 
            new Vector3(-0.425325f, -0.688191f, -0.587785f), 
            new Vector3(-0.587785f, -0.425325f, -0.688191f), 
            new Vector3(-0.688191f, -0.587785f, -0.425325f)
        };
    }
}
