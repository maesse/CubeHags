using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.map.Source;
using SlimDX;
using System.IO;
using CubeHags.client.common;
using CubeHags.server;

namespace CubeHags.common
{
    public sealed partial class ClipMap
    {
        private static readonly ClipMap _Instance = new ClipMap();
        public static ClipMap Instance {get{return _Instance;}}


        string       name;

    	int			numBrushSides;
    	List<dbrushside_t> brushsides = new List<dbrushside_t>();

    	List<cplane_t>	planes = new List<cplane_t>();

    	int			numNodes;
    	public dnode_t[]	nodes;

    	int			numLeafs;
    	dleaf_t[]	leafs;

    	int			numLeafBrushes;
    	List<int>		leafbrushes = new List<int>();

    	List<int>		leafFaces = new List<int>();

    	int			numSubModels;
    	cmodel_t[]	cmodels;

    	List<dbrush_t>	brushes = new List<dbrush_t>();

    	int			numClusters;
    	int			clusterBytes;
    	byte[]		visibility;
    	bool	    vised;			// if false, visibility is just a single cluster of ffs

    	string		entityString;

    	int			numAreas;
    	//cArea_t		*areas;
    	//int			*areaPortals;	// [ numAreas*numAreas ] reference counts

    	int			numSurfaces;

    	int			floodvalid;
    	public int			checkcount;					// incremented on each trace
        public int c_traces;
        int c_brush_traces;

        cmodel_t box_model;
        List<cplane_t> box_planes = new List<cplane_t>();
        dbrush_t box_brush;
        dvis_t vis;

        CVar cm_noAreas = CVars.Instance.Get("cm_noAreas", "1", CVarFlags.TEMP);

        ClipMap()
        {

        }

        public bool AreasConnected(int area1, int area2)
        {
            if (cm_noAreas.Integer == 1)
                return true;

            if (area1 < 0 || area2 < 0)
                return false;

            if (area1 >= numAreas || area2 >= numAreas)
                Common.Instance.Error("area >= numAreas");

            // FIX: Add areas
            return false;
        }


        /*
        =================
        CM_WriteAreaBits

        Writes a bit vector of all the areas
        that are in the same flood as the area parameter
        Returns the number of bytes needed to hold all the bits.

        The bits are OR'd in, so you can CM_WriteAreaBits from multiple
        viewpoints and get the union of all visible areas.

        This is used to cull non-visible entities from snapshots
        =================
        */
        public int WriteAreaBits(ref byte[] buffer, int area)
        {
            int bytes = (numAreas + 7) >> 3;

            if (cm_noAreas.Integer == 1 || area == -1)
            {
                // for debugging, send everything
                for (int i = 0; i < bytes; i++)
                {
                    buffer[i] = 255;
                }
            }
            else
            {
                //int floodnum = areas
            }

            return bytes;
        }

        public bool[] ClusterPVS(int cluster)
        {
            if (cluster < 0 || cluster >= numClusters || !vised)
                return null;

            return GetPVS(cluster);
        }



        // current cluster, cluster to test against
        private bool[] GetPVS(int visCluster)
        {
            int i = visCluster;// (visCluster >> 3);
            if (visCluster < 0 || visibility == null)
            {
                return null;
            }

            int v = vis.byteofs[i][0]; // offset into byte-vector
            bool[] output = new bool[numClusters];
            for (int c = 0; c < numClusters; v++)
            {
                if (visibility[v] == (byte)0)
                {
                    // Skip
                    c += 8 * visibility[++v];
                }
                else
                {
                    // Add visible info
                    for (byte bit = 1; bit != 0; bit *= 2, c++)
                    {
                        if ((visibility[v] & bit) != 0)
                        {
                            output[c] = true;
                        }
                    }
                }
            }

            return output;
        }

        public int LeafCluster(int leafnum)
        {
            if (leafnum < 0 || leafnum > numLeafs)
            {
                Common.Instance.Error("LeafCluster: Bad number");
            }
            return leafs[leafnum].cluster;
        }

        public int LeafArea(int leafnum)
        {
            if (leafnum < 0 || leafnum >= numLeafs)
            {
                Common.Instance.Error("LeafArea: Bad number");
            }
            return leafs[leafnum].area;
        }

        public int PointLeafnum(Vector3 org)
        {
            if (nodes == null)
                return 0;
            
            int num = 0;
            dnode_t node;
            cplane_t plane;
            float d;
            while (num >= 0)
            {
                node = nodes[num];
                plane = node.plane;

                if (plane.type < 3)
                {
                    d = org[plane.type] - plane.dist;
                }
                else
                {
                    d = Vector3.Dot(plane.normal, org) - plane.dist;
                }
                if(d < 0f)
                    num = node.children[1];
                else
                    num = node.children[0];
            }

            return -1 - num;
        }

        public void ModelBounds(int index, ref Vector3 mins, ref Vector3 maxs)
        {
            if (index < 0)
                return;

            if (index > numSubModels)
            {
                mins = cmodels[index].mins;
                maxs = cmodels[index].maxs;
            }
            else if (index == 255)
            {
                mins = box_model.mins;
                maxs = box_model.maxs;
            }
        }

        public void LoadMap(string filename, bool clientLoad)
        {
            if (filename == null || filename.Length == 0)
            {
                Common.Instance.WriteLine("CM_LoadMap: Null name");
                throw new Exception("null name map");
            }
            if (filename.Equals(name) && clientLoad)
            {
                return;
            }

            ClearMap();

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
                return;
            }
            
            BinaryReader br = new BinaryReader(stream, Encoding.UTF8);

            // Read header..
            int magic = (('P' << 24) + ('S' << 16) + ('B' << 8) + 'V');
            int id = br.ReadInt32();
            if (id != magic)
            {
                Common.Instance.WriteLine("[BSPParser] Wrong magic number for file {0}", filename);
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

            // Parse lumps
            ReadLeafs(sHeader, br);
            ReadLeafBrushes(sHeader, br);
            ReadLeafFaces(sHeader, br);
            ReadPlanes(sHeader, br);
            ReadBrushSides(sHeader, br);
            ReadBrushes(sHeader, br);
            ReadModels(sHeader, br);
            ReadNodes(sHeader, br);
            ReadEntityString(sHeader, br);
            ReadVisibility(sHeader, br);

            stream.Close();
            stream.Dispose();

            InitHullBox();

            name = filename;
        }

        public void ClearMap()
        {

            //_Instance = new ClipMap();
        }

        /*
        ===================
        CM_InitBoxHull

        Set up the planes and nodes so that the six floats of a bounding box
        can just be stored out and get a proper clipping hull structure.
        ===================
        */
        void InitHullBox()
        {
            //box_planes = 

            dbrush_t boxbrush = new dbrush_t();
            boxbrush.numsides = 6;
            boxbrush.contents = brushflags.CONTENTS_PLAYERCLIP;
            brushes.Add(boxbrush);
            box_brush = boxbrush;

            box_model = new cmodel_t();
            box_model.leaf = new dleaf_t();
            box_model.leaf.numleafbrushes = 1;
            box_model.leaf.firstleafbrush = (ushort)leafbrushes.Count;
            leafbrushes.Add(brushes.Count);

            cplane_t[] plan = new cplane_t[6*2];
            for (int i = 0; i < 12; i++)
            {
                plan[i] = new cplane_t();
            }

            dbrushside_t[] sides = new dbrushside_t[6];
            for (int i = 0; i < 6; i++)
            {
                sides[i] = new dbrushside_t();
            }

            int numBrushSides = brushsides.Count;
            for (int i = 0; i < 6; i++)
            {
                int side = i & 1;
                dbrushside_t s = sides[i];
                s.plane = plan[i * 2 + side];

                // planes
                cplane_t p = plan[i * 2];
                p.type = i >> 1;
                p.signbits = 0;
                p.normal = Vector3.Zero;
                p.normal[i >> 1] = 1;

                p = planes[i * 2 + 1];
                p.type = 3 + (i >> 1);
                p.signbits = 0;
                p.normal = Vector3.Zero;
                p.normal[i >> 1] = -1;

                Common.SetPlaneSignbits(p);
            }
            
            box_planes.AddRange(plan);
            planes.AddRange(plan);
            brushsides.AddRange(sides);
        }

        void ReadVisibility(Header header, BinaryReader br)
        {
            // Read Vis
            br.BaseStream.Seek(header.lumps[4].fileofs, SeekOrigin.Begin);
            int nVis = header.lumps[4].filelen / 68;
            if (nVis > 0)
            {
                vis = new dvis_t();
                vis.numclusters = br.ReadInt32();
                vis.byteofs = new int[vis.numclusters][];
                for (int j = 0; j < vis.numclusters; j++)
                {
                    vis.byteofs[j] = new int[] { br.ReadInt32(), br.ReadInt32() };
                }
                numClusters = vis.numclusters;
                // VisData
                br.BaseStream.Seek(header.lumps[4].fileofs, SeekOrigin.Begin);
                int nVisData = (int)(header.lumps[4].filelen);
                visibility = new byte[nVisData];
                for (int i = 0; i < nVisData; i++)
                {
                    visibility[i] = br.ReadByte();
                }
                vised = true;
            }
            else
            {
                int clusterBytes = (numClusters +31 )& ~31;
                visibility = new byte[clusterBytes];
                for (int i = 0; i < clusterBytes; i++)
                {
                    visibility[i] = 255;
                }
            }
        }

        void ReadEntityString(Header header, BinaryReader br)
        {
            // Entities
            br.BaseStream.Seek(header.lumps[0].fileofs, SeekOrigin.Begin);
            int nEntities = header.lumps[0].filelen;
            StringBuilder entitiesBuilder = new StringBuilder(nEntities);
            entitiesBuilder.Append(br.ReadChars(nEntities));
            entityString = entitiesBuilder.ToString();
            //Server.Instance.sv.entityParseString = entityString;
            string[] lines = entityString.Split('\n');
            Server.Instance.sv.entityParsePoint = 0;
            Server.Instance.sv.entityParseString = lines;
             //map.entities = Entity.CreateEntities(entitiesBuilder.ToString());
        }

        void ReadNodes(Header header, BinaryReader br)
        {
            // Read nodes
            br.BaseStream.Seek(header.lumps[5].fileofs, SeekOrigin.Begin);
            int nNodes = header.lumps[5].filelen / 32;
            nodes = new dnode_t[nNodes];
            for (int i = 0; i < nNodes; i++)
            {
                dnode_t node = new dnode_t();
                node.planenum = br.ReadInt32();
                node.children = new int[] { br.ReadInt32(), br.ReadInt32() };
                node.mins = SourceParser.SwapZY(new Vector3(br.ReadInt16(), br.ReadInt16(), br.ReadInt16())); // 3 For frustrum culling
                node.maxs = SourceParser.SwapZY(new Vector3(br.ReadInt16(), br.ReadInt16(), br.ReadInt16())); // 3
                node.firstface = br.ReadUInt16(); // index into face array
                node.numfaces = br.ReadUInt16();  // counting both sides
                node.area = br.ReadInt16();     // If all leaves below this node are in the same area, then
                // this is the area index. If not, this is -1.
                node.paddding = br.ReadInt16(); 	 // pad to 32 bytes length

                node.plane = planes[node.planenum];

                nodes[i] = node;
            }
        }

        void ReadModels(Header header, BinaryReader br)
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

            cmodels = new cmodel_t[models.Length];

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
                mout.leaf.firstleafface = (ushort)leafFaces.Count;
                for (int j = 0; j < min.numfaces; j++)
                {
                    leafFaces.Add(min.firstface + j);
                }

                cmodels[i] = mout;
            }
        }

        void ReadBrushSides(Header header, BinaryReader br)
        {
            // read brushsides
            br.BaseStream.Seek(header.lumps[19].fileofs, SeekOrigin.Begin);
            int numBrushSides = header.lumps[19].filelen / 8;
            for (int i = 0; i < numBrushSides; i++)
            {
                dbrushside_t brushside = new dbrushside_t();
                brushside.planenum = br.ReadUInt16();
                brushside.plane = planes[brushside.planenum];
                brushside.texinfo = br.ReadInt16();
                brushside.dispinfo = br.ReadInt16();
                brushside.bevel = br.ReadInt16();

                brushsides.Add(brushside);
            }
        }

        void ReadBrushes(Header header, BinaryReader br)
        {
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
                    brush.sides[j] = brushsides[brush.firstside + j];
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
                brushes.Add(brush);
            }
        }

        void ReadPlanes(Header header, BinaryReader br)
        {
            // Read planes
            br.BaseStream.Seek(header.lumps[1].fileofs, SeekOrigin.Begin);
            int numPlanes = header.lumps[1].filelen / 20;
            for (int i = 0; i < numPlanes; i++)
            {
                cplane_t plane = new cplane_t();
                plane.normal = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
                plane.dist = br.ReadSingle();
                plane.type = br.ReadInt32();
                int bits = 0;
                for (int j = 0; j < 3; j++)
                {
                    if (plane.normal[j] < 0)
                        bits |= 1 << j;
                }
                plane.signbits = (byte)bits;
                planes.Add(plane);
            }
        }

        void ReadLeafFaces(Header header, BinaryReader br)
        {
            // read leafFaces
            br.BaseStream.Seek(header.lumps[16].fileofs, SeekOrigin.Begin);
            leafFaces = new List<int>(header.lumps[16].filelen / 2);
            for (int i = 0; i < header.lumps[16].filelen / 2; i++)
            {
                leafFaces.Add(br.ReadUInt16());
            }
        }

        void ReadLeafBrushes(Header header, BinaryReader br)
        {
            // read leafBrushes
            br.BaseStream.Seek(header.lumps[17].fileofs, SeekOrigin.Begin);
            int numLeafBrushes = header.lumps[17].filelen;
            for (int i = 0; i < numLeafBrushes; i++)
            {
                leafbrushes.Add((int)br.ReadInt16());
            }
        }

        void ReadLeafs(Header header, BinaryReader br)
        {
            // Determine size
            int leafSize = 56;
            if (header.version == 20 || header.version == 17)
            {
                if (header.lumps[10].filelen % 32 == 0)
                    leafSize = 32;
                else
                    System.Console.WriteLine("Problem reading leafs..");
            }

            numLeafs = header.lumps[10].filelen / leafSize;
            leafs = new dleaf_t[numLeafs];
            for (int i = 0; i < numLeafs; i++)
            {
                br.BaseStream.Seek(header.lumps[10].fileofs + (i * leafSize), SeekOrigin.Begin);
                dleaf_t leaf = new dleaf_t();
                leaf.contents = br.ReadInt32();
                leaf.cluster = br.ReadInt16();
                if (leaf.cluster > numClusters)
                    numClusters = leaf.cluster + 1;
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
                        float r = SourceParser.TexLightToLinear((int)color.r, color.exp ) * 255;
                        float g = SourceParser.TexLightToLinear((int)color.g, color.exp ) * 255;
                        float b = SourceParser.TexLightToLinear((int)color.b, color.exp ) * 255;
                        leaf.ambientLighting.Color[j] = new Vector3(r, g, b);
                    }
                }
                //else if (map.HDRCubes != null && i < map.HDRCubes.Length)
                //{
                //    leaf.ambientLighting = map.HDRCubes[i];
                //}
                //else if (map.LDRCubes != null && i < map.LDRCubes.Length)
                //{
                //    leaf.ambientLighting = map.LDRCubes[i];
                //}
                //else
                //{
                //    System.Console.WriteLine("No ambient cube for this leaf :(");
                //}
                leaf.padding = 0;//br.ReadInt16();
                leaf.staticProps = new List<SourceProp>();
                leafs[i] = leaf;
            }

            numAreas = 0;
        }
    }

    public struct cmodel_t 
    {
    	public Vector3		mins, maxs;
        public dleaf_t leaf;			// submodels don't reference the main tree
    }
}
