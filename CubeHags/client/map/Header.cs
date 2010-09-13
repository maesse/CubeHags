using System;
using System.Collections.Generic;
 
using System.Text;

using SlimDX;
using SlimDX.Direct3D9;
using CubeHags.common;

namespace CubeHags.client.map.Source
{

    

    
    
    public struct Header
    {
        public static int HEADER_LUMPS = 64;
        public int ident;
        public int version;
        public Lump_t[] lumps; // HEADER_LUMPS
        public int mapRevision;
    }

    [Flags]
    public enum SurfFlags : int
    {
        SURF_LIGHT = 0x0001,     // value will hold the light strength
        SURF_SLICK = 0x0002,		// effects game physics
        SURF_SKY = 0x0004,		// don't draw, but add to skybox
        SURF_WARP = 0x0008,		// turbulent water warp
        SURF_TRANS = 0x0010,     // surface is transparent
        SURF_WET = 0x0020,		// the surface is wet
        SURF_FLOWING = 0x0040,		// scroll towards angle
        SURF_NODRAW = 0x0080,		// don't bother referencing the texture
        SURF_HINT = 0x0100,		// make a primary bsp splitter
        SURF_SKIP = 0x0200,		// completely ignore, allowing non-closed brushes
        SURF_NOLIGHT = 0x0400,		// Don't calculate light on this surface
        SURF_BUMPLIGHT = 0x0800,		// calculate three lightmaps for the surface for bumpmapping
        SURF_NOSHADOWS = 0x1000,		// Don't receive shadows
        SURF_NODECALS = 0x2000,		// Don't receive decals
        SURF_NOCHOP = 0x4000,		// Don't subdivide patches on this surface 
        SURF_HITBOX = 0x8000		// surface is part of a hitbox
    };

    public struct Lump_t
    {
        public int fileofs;
        public int filelen;
        public int version;
        public char[] fourCC; // 4
    }

    public struct edge_t
    {
          public ushort[]    v;       //  2 vertex indices
    };

    public class Face : IComparable<Face>
    {
        public cplane_t plane_t;		// the plane number
        public texinfo_t texinfo;			// texture info
        public face_t face_t;
        public Texture lightmap;
        public int lightOffsetX, lightOffsetY;
        //public int light2X, light2Y, light3X, light3Y, light4X, light4Y;

        public VertexBuffer vb = null;
        public int nVerts;
        public int VertexOffset = -1;

        public uint[] indices;
        public VertexFormat Format;
        public RenderItem item = null;
        public int[] DisplaceFaces;

        public bool HasDisplacement = false;
        public int displace_offset;
        public int nDisplace;
        public int lastVisCount;
        public Vector3[] BBox;

        #region IComparable<Face> Members

        int IComparable<Face>.CompareTo(Face other)
        {
            int size = face_t.LightmapTextureSizeInLuxels[0]+1 * face_t.LightmapTextureSizeInLuxels[1]+1;
            int otherSize = other.face_t.LightmapTextureSizeInLuxels[0]+1 * face_t.LightmapTextureSizeInLuxels[1]+1;

            return size.CompareTo(otherSize);
        }

        #endregion
    }

    public struct face_t
    {
	    public ushort    planenum;		// the plane number
	    public byte        side;			// faces opposite to the node's plane direction
	    public byte        onNode; 			// 1 of on node, 0 if in leaf
	    public int         firstedge;			// index into surfedges	
	    public short       numedges;			// number of surfedges
	    public short       texinfo;			// texture info
	    public short       dispinfo;			// displacement info
	    public short       surfaceFogVolumeID;		// ?	
	    public byte[]        styles;			// 4 switchable lighting info
	    public int         lightlumpofs;			// offset into lightmap lump
	    public float       area;				// face area in units^2
	    public int[]         LightmapTextureMinsInLuxels;   // 2 texture lighting info
        public int[] LightmapTextureSizeInLuxels;   //2  texture lighting info
	    public int         origFace;			// original face this was split from
	    public ushort    numPrims;		// primitives
	    public ushort    firstPrimID; 
	    public uint      smoothingGroups;	// lightmap smoothing group
        public Face face;
    };

    public struct texinfo_t
    {
        public Vector4[] textureVecs;      //2][4 [s/t][xyz offset]
        //public Vector4[] lightmapVecs;     //2][4 [s/t][xyz offset] - length is in units of texels/area
        public Vector3[] lightmapVecs;
        public float[] lightmapVecs2;
        public SurfFlags flags;                  // miptex flags + overrides
        public int texdata;                // Pointer to texture name, size, etc.
        public texdata_t texdata_t;
    }

    //public class Texinfo
    //{
    //    public Vector4[] textureVecs;      //2][4 [s/t][xyz offset]
    //    public Vector4[] lightmapVecs;     //2][4 [s/t][xyz offset] - length is in units of texels/area
    //    public Texdata texdata;                // Pointer to texture name, size, etc.
    //    public texinfo_t texinfo_t;
    //}

    public class plane_t
    {
        public Vector3 normal;     // normal vector
        public float dist;       // distance from origin
        public int type;       // plane axis identifier
    };

    //public class Texdata
    //{
    //    public string name;      // index into TexdataStringTable
    //    public SourceMaterial mat;
    //    public texdata_t texdata_t;
    //}

    public struct texdata_t
    {
        public Vector3 reflectivity;           // RGB reflectivity 	
        public int nameStringTableID;      // index into TexdataStringTable
        public string name;
        public int width, height;         	// source image
        public int view_width, view_height;
        public SourceMaterial mat;
    };


    // 4 bytes
    public struct RGBExp
    {
        public byte r, g, b;
        public sbyte exp;
    };

    // plane_t structure
    // !!! if this is changed, it must be changed in asm code too !!!
    public class cplane_t {
    	public Vector3	normal;
        public float dist;
        public int type;			// for fast side tests: 0,1,2 = axial, 3 = nonaxial
        public byte signbits;		// signx + (signy<<1) + (signz<<2), used as lookup during collision

        public override string ToString()
        {
            return "Pl: n" + normal + " d" + dist + " t" + type;
        }
    }

    public struct ddispinfo_t
    {
        public Vector3 startPosition;          // start position used for orientation
        public int DispVertStart;          // Index into LUMP_DISP_VERTS.
        public int DispTriStart;           // Index into LUMP_DISP_TRIS.
        public int power;                 // power - indicates size of surface (2^power + 1)
        public int minTess;                // minimum tesselation allowed
        public float smoothingAngle;         // lighting smoothing angle
        public int contents;               // surface contents
        public ushort MapFace;          // Which map face this displacement comes from.
        public int LightmapAlphaStart;     // Index into ddisplightmapalpha.
        public int LightmapSamplePositionStart;  	// Index into LUMP_DISP_LIGHTMAP_SAMPLE_POSITIONS.
        public DisplaceNeighbor[] EdgeNeighbors; 	// 4 Indexed by NEIGHBOREDGE_ defines.
        public DisplaceCornerNeighbor[] CornerNeighbors;     // 4 Indexed by CORNER_ defines.
        public ulong[] AllowedVerts;    // ALLOWEDVERTS_SIZE active verticies
    };

    public class dDispVert
    {
        public Vector3 vec;        // Vector field defining displacement volume.
        public float dist;       // Displacement distances.
        public float alpha;      // "per vertex" alpha values.
    };

    public class dDispTri
    {
        public ushort Tags;          // Displacement triangle tags.
    };

    public struct DisplaceSubNeighbor
{
        public ushort neighbor_index;
        public byte neighbor_orient;
        public byte local_span;
        public byte neighbor_span;
	};


    public struct DisplaceNeighbor
	{
        public DisplaceSubNeighbor[] sub_neighbors; // 2
	};


    public struct DisplaceCornerNeighbor
	{
        public ushort[] neighbor_indices; // 4
        public byte neighbor_count;
	};


    //// 20 bytes
    //public struct dplane_t
    //{
    //    public Vector3 normal;
    //    public float dist;
    //    public int type;
    //}

    public class dnode_t
    {
        public cplane_t plane;
        public int planenum; // index into plane array
        public int[] children; // 2 negative numbers are -(leafs+1), not nodes
        public Vector3 mins; // 3 For frustrum culling
        public Vector3 maxs; // 3
        public ushort    firstface; // index into face array
        public ushort numfaces;  // counting both sides
        public short area;    // If all leaves below this node are in the same area, then
		        			 // this is the area index. If not, this is -1.
        public short paddding;	 // pad to 32 bytes length
        public object parent;
        public int lastVisibleCount;
    }

    public class dleaf_t
    {
        public int               contents;               // OR of all brushes (not needed?)
	    public short             cluster;                // cluster this leaf is in
	    public short             area; // =9;                 // area this leaf is in
	    public short             flags;// =7;                // flags
	    public Vector3             mins;                //  3 for frustum culling
        public Vector3 maxs; // 3
	    public ushort           firstleafface;          // index into leaffaces
	    public ushort           numleaffaces;
	    public ushort           firstleafbrush;         // index into leafbrushes
	    public ushort           numleafbrushes;
	    public short             leafWaterDataID;        // -1 for not in water
	    public CompressedLightCube     ambientLighting;  // Precaculated light info for entities.
	    public short             padding;                // padding to 4-byte boundary
        public int lastVisibleCount;
        public dnode_t parent;
        public List<SourceProp> staticProps;
        public KeyValuePair<int, int>[] DisplacementIndexes;
        public DispLeafLink m_pDisplacements;
    }

    public struct LightCube
    {
        public Vector3 AmbientCubeX;
        public Vector3 AmbientCubeX2;
        public Vector3 AmbientCubeY;
        public Vector3 AmbientCubeY2;
        public Vector3 AmbientCubeZ;
        public Vector3 AmbientCubeZ2;
    }
    public struct CompressedLightCube
    {
        public Vector3[] Color;
	    //public RGBExp[] m_Color; // 6
    };

    public struct dvis_t
    {
        public int numclusters;
        public int[][] byteofs; // numclusters, 2
    }

    // lump 14 - 24 bytes
    public struct dmodel_t
    {
        public Vector3 mins, maxs;             // bounding box
        public Vector3 origin;                 // for sounds or lights
        public int headnode;               // index into node array
        public int firstface, numfaces;    // index into face array
    }

    public struct dbrush_t
    {
        public int firstside;
        public int numsides;
        public brushflags contents;
        public Vector3 boundsmin, boundsmax;
        public dbrushside_t[] sides;
        public int checkcount;
    }

    [Flags]
    public enum brushflags
    {
        CONTENTS_EMPTY =         0,           // No contents
        CONTENTS_SOLID        =  0x1,         // an eye is never valid in a solid
        CONTENTS_WINDOW       =  0x2,         // translucent, but not watery (glass)
        CONTENTS_AUX          =  0x4,
        CONTENTS_GRATE        =  0x8,         // alpha-tested "grate" textures.  Bullets/sight pass through, but solids don't
        CONTENTS_SLIME        =  0x10,
        CONTENTS_WATER        =  0x20,
        CONTENTS_MIST         =  0x40,
        CONTENTS_OPAQUE       =  0x80,  	// things that cannot be seen through (may be non-solid though)
        CONTENTS_TESTFOGVOLUME=  0x100,   // can see into a fogvolume (water)
        CONTENTS_MOVEABLE     =  0x4000,
        CONTENTS_AREAPORTAL   =  0x8000,
        CONTENTS_PLAYERCLIP   =  0x10000,
        CONTENTS_MONSTERCLIP  =  0x20000,
        CONTENTS_CURRENT_0    =  0x40000,
        CONTENTS_CURRENT_90   =  0x80000,
        CONTENTS_CURRENT_180  =  0x100000,
        CONTENTS_CURRENT_270  =  0x200000,
        CONTENTS_CURRENT_UP   =  0x400000,
        CONTENTS_CURRENT_DOWN =  0x800000,
        CONTENTS_ORIGIN       =  0x1000000,   // removed before bsping an entity
        CONTENTS_MONSTER      =  0x2000000,   // should never be on a brush, only in game
        CONTENTS_DEBRIS       =  0x4000000,
        CONTENTS_DETAIL       =  0x8000000,   // brushes to be added after vis leafs
        CONTENTS_TRANSLUCENT  =  0x10000000,  // auto set if any surface has trans
        CONTENTS_LADDER       =  0x20000000,
        CONTENTS_HITBOX = 0x40000000  // use accurate hitboxes on trace

    };

    public class dbrushside_t
    {
        public ushort planenum; // facing out of the leaf
        public cplane_t plane;
        public short texinfo;   // texture info
        public short dispinfo;  // displacement info
        public short bevel;     // is the side a bevel plane?
    }

    public struct dgamelumpheader_t
    {
        public int lumpCount;
        public dgamelump_t[] gamelump;
    }

    public struct dgamelump_t
    {
        public int id;
        public ushort flags;
        public ushort version;
        public int fileofs;
        public int filelen;
    }

    public struct StaticPropDictLump_t
    {
        public int dictEntries;
        public char[] name; // model name
        public string[] Name;
    }

    public struct StaticPropLeafLump_t
    {
        public int leafEntries;
        public ushort[] leaf;
    }

    public struct StaticPropLump_t
    {
        public Vector3 Origin;          // origin
        public Vector3 Angles;          // orientation (pitch roll yaw)
        public ushort PropType;         // index into model name dictionary
        public string PropName;
        public ushort FirstLeaf;        // index into leaf array
        public ushort LeafCount;
        public byte Solid;              // solidity type
        public byte Flags;
        public int Skin;                // model skin numbers
        public float FadeMinDist;
        public float FadeMaxDist;

        public Vector3 LightingOrigin;  // for lighting 
        public float ForcedFaceScale;   // only present in version 5 gamelump
        public int lastVisibleCount;
        public ushort lastVisibleLeaf;
    }

    public enum EmitType : int    // lights that were used to illuminate the world
    {
        SURFACE = 0,    // 90 degree spotlight
        POINT,          // simple point light source
        SPOTLIGHT,      // spotlight with penumbra
        SKYLIGHT,       // directional light with no falloff (surface must trace to SKY texture)
        QUAKELIGHT,     // linear falloff, non-lambertian
        SKYAMBIENT      // spherical light source with no falloff (surface must trace to SKY texture)
    }

    public class worldlight_t
    {
        public Vector3 Origin;
        public Vector3 Intensity;
        public Vector3 Normal;  // for surfaces and spotlights
        public int Cluster;
        public EmitType Type;
        public int Style;
        public float stopdot;   // start of penumbra for emit_spotlight
        public float stopdot2;  // end of penumbra for emit_spotlight
        public float exponent;
        public float radius;    // cutoff distance
        // falloff for emit_spotlight + emit_point: 
        // 1 / (constant_attn + linear_attn * dist + quadratic_attn * dist^2)
        public float Constant_Attn;
        public float Linear_Attn;
        public float Quadratic_Attn;
        public int Flags;
        public int Texinfo;
        public int Owner;   // entity that this light it relative to
    }

}
