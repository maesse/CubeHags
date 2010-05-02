using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace CubeHags.client.map.Source
{
    class VVDReader
    {
        public static List<VVDVertex>[] vertex_buffer;

        public static void ReadFile(string filename)
        {
            using (FileStream fs = File.OpenRead(filename))
            {
                BinaryReader br = new BinaryReader(fs);

                // Read and check header
                VVDHeader header = ReadHeader(br);
                if (header.magic_number != VVDHeader.VVD_MAGIC_NUMBER)
                {
                    System.Console.WriteLine("[VVDReader] Magic number invalid (file:{0})", filename);
                    return;
                }

                // Read fixup table
                VVDFixupEntry[] fixupTable = new VVDFixupEntry[header.num_fixups];
                br.BaseStream.Seek(header.fixup_table_offset, SeekOrigin.Begin);
                for (int i = 0; i < header.num_fixups; i++)
                {
                    fixupTable[i] = ReadFixupEntry(br);
                }

                vertex_buffer = new List<VVDVertex>[8];
                int[] vertex_buffer_size = new int[8];

                // Create vertex buffers
                for (int i = 0; i < header.num_lods; i++)
                {
                    // Create vb for this LOD
                    vertex_buffer[i] = new List<VVDVertex>(header.num_lod_verts[i]);
                    vertex_buffer_size[i] = header.num_lod_verts[i];

                    // Check for fixups
                    if (header.num_fixups > 0)
                    {
                        // Scan fixup table for this LOD
                        for (int j = 0; j < header.num_fixups; j++)
                        {
                            // check lod level
                            if (fixupTable[j].lod_number >= i)
                            {
                                // Seek
                                br.BaseStream.Seek(header.vertex_data_offset + (fixupTable[j].source_vertex_id * 48), SeekOrigin.Begin);

                                // Read
                                for (int h = 0; h < fixupTable[j].num_vertices; h++)
                                {
                                    vertex_buffer[i].Add(ReadVertex(br));
                                }
                            }
                        }
                    }
                    else
                    {
                        // Seek to vertex data
                        br.BaseStream.Seek(header.vertex_data_offset, SeekOrigin.Begin);

                        // Read
                        for (int h = 0; h < header.num_lod_verts[i]; h++)
                        {
                            vertex_buffer[i].Add(ReadVertex(br));
                        }
                    }

                    // Scale to  meters
                    if (false)
                    {
                        for (int j = 0; j < vertex_buffer_size[i]; j++)
                        {
                            //vertex_buffer[i][j].vertex_position *= 0.0254;
                        }
                    }
                }
            }
        }

        public static VVDVertex ReadVertex(BinaryReader br)
        {
            VVDVertex vertex = new VVDVertex();
            vertex.bone_weights = new VVDBoneWeight();
            vertex.bone_weights.weight = new float[] {br.ReadSingle(),br.ReadSingle(),br.ReadSingle()}; // 3
            vertex.bone_weights.bone = br.ReadChars(3);
            vertex.bone_weights.num_bones = br.ReadByte(); 
            vertex.vertex_position = SourceParser.SwapZY(new SlimDX.Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle()));
            vertex.vertex_normal = SourceParser.SwapZY(new SlimDX.Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle()));
            vertex.vertex_texcoord = new SlimDX.Vector2(br.ReadSingle(),br.ReadSingle());
            return vertex;
        }

        public static VVDFixupEntry ReadFixupEntry(BinaryReader br)
        {
            VVDFixupEntry entry = new VVDFixupEntry();
            entry.lod_number = br.ReadInt32();
            entry.source_vertex_id = br.ReadInt32();
            entry.num_vertices = br.ReadInt32();

            return entry;
        }

        public static VVDHeader ReadHeader(BinaryReader br)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);

            VVDHeader header = new VVDHeader();
            header.magic_number = br.ReadInt32();
            header.vvd_version = br.ReadInt32();
            header.check_sum = br.ReadInt32();
            header.num_lods = br.ReadInt32();
            header.num_lod_verts = new int[8]; // 8
            for (int i = 0; i < 8; i++)
            {
                header.num_lod_verts[i] = br.ReadInt32();
            }
            header.num_fixups = br.ReadInt32();
            header.fixup_table_offset = br.ReadInt32();
            header.vertex_data_offset = br.ReadInt32();
            header.tangent_data_offset = br.ReadInt32();

            return header;
        }
    }
}
