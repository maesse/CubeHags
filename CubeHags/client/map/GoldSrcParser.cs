using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using CubeHags.client.map.Source;



namespace CubeHags.client.map
{
    public static class GoldSrcParser
    {
        public enum Lumps : int
        {
            LUMP_ENTITIES =	0,
            LUMP_PLANES	=	1,
            LUMP_TEXTURES=	2,
            LUMP_VERTEXES=	3,
            LUMP_VISIBILITY=	4,
            LUMP_NODES	=	5,
            LUMP_TEXINFO=	6,
            LUMP_FACES	=	7,
            LUMP_LIGHTING=	8,
            LUMP_CLIPNODES=	9,
            LUMP_LEAFS	=	10,
            LUMP_MARKSURFACES= 11,
            LUMP_EDGES	=	12,
            LUMP_SURFEDGES=	13,
            LUMP_MODELS	=	14,
            LUMP_COUNT = 15
        }
        
        public static void LoadWorldMap(Header header, BinaryReader br)
        {
            header.lumps = new Lump_t[(int)Lumps.LUMP_COUNT];
            for (int i = 0; i < Header.HEADER_LUMPS; i++)
            {
                Lump_t lump = new Lump_t();
                lump.fileofs = br.ReadInt32();
                lump.filelen = br.ReadInt32();
                header.lumps[i] = lump;
            }

            // read edge_t


        }
    }
}
