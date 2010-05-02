using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SlimDX;
using CubeHags.client.render.Formats;
using SlimDX.Direct3D9;
using CubeHags.client.render;

namespace CubeHags.client.map.Source
{
    class VTXReader
    {
        public static void ReadFile(string filename, SourceModel mdl)
        {
            using (FileStream fs = File.OpenRead(filename))
            {
                BinaryReader br = new BinaryReader(fs);
                VTXHeader header = ReadHeader(br);

                // Process body pars
                for (int i = 0; i < header.num_body_parts; i++)
                {
                    // get corresponding bodypart from mdl tree
                    BodyPart currentPart = mdl.BodyParts[i];

                    // Process bodypart
                    ReadBodyPart(br, header.body_part_offset + (i* 8), currentPart); // 8 bytes for VTXBodypart
                }
            }
        }

        public static void ReadBodyPart(BinaryReader br, int offset, BodyPart bodypart)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);

            // Read bodypart
            VTXBodyPart vtxbody = new VTXBodyPart();
            vtxbody.num_models = br.ReadInt32();
            vtxbody.model_offset = br.ReadInt32();

            //// If there is more than one model, create a switch to select between them
            //// (it seems that only one model is supposed to be seen at a given time,
            //// but I don't know the mechanism in the engine that selects a desired
            //// model)
            //if (part.num_models > 1)
            //    partSwitch = new Switch();

            // Process models
            for (int i = 0; i < vtxbody.num_models; i++)
            {
                Model currentModel = bodypart.Models[i];
                ReadModel(br, offset + vtxbody.model_offset + (i * 8), currentModel); // 8 bytes for VTXModel
            }
        }

        public static VTXModel ReadModel(BinaryReader br, int offset, Model currentModel)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            VTXModel model = new VTXModel();
            model.num_lods = br.ReadInt32();
            model.lod_offset = br.ReadInt32();

            // Check for multiple LOD-levels
            if (model.num_lods >= 1)
            {
                // TODO
            }

            float distance = 0f;
            float lastDistance = 0f;
            // Process LODS
            for (int i = 0; i < model.num_lods; i++)
            {
                ReadModelLOD(br, offset + model.lod_offset + (i * 12), currentModel, i); // 12 bytes for VYXModelLOD
            }

            return model;
        }

        public static VTXModelLOD ReadModelLOD(BinaryReader br, int offset, Model currentModel, int lodNum)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            VTXModelLOD mlod = new VTXModelLOD();
            mlod.num_meshes = br.ReadInt32();
            mlod.mesh_offset = br.ReadInt32();
            mlod.switch_point = br.ReadSingle();

            int vertexOffset = currentModel.VertexBaseNum;
            for (int i = 0; i < mlod.num_meshes; i++)
            {
                MDLMesh currentMesh = currentModel.Meshes[i];
                ReadMesh(br, offset + mlod.mesh_offset + (i * VTXMesh.VTX_MESH_SIZE), lodNum, vertexOffset, currentMesh);
            }

            return mlod;
        }

        public static VTXMesh ReadMesh(BinaryReader br, int offset, int lodNum, int vertexOffset, MDLMesh mdlmesh)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            VTXMesh mesh = new VTXMesh();
            mesh.num_strip_groups = br.ReadInt32();
            mesh.strip_group_offset = br.ReadInt32();
            mesh.mesh_flags = br.ReadByte();
            
            for (int i = 0; i < mesh.num_strip_groups; i++)
            {
                ReadStripGroup(br, offset + mesh.strip_group_offset + (i * VTXStripGroup.VTX_STRIP_GROUP_SIZE), lodNum, vertexOffset, mdlmesh);
            }

            return mesh;
        }

        public static VTXStripGroup ReadStripGroup(BinaryReader br, int offset, int lodNum, int vertexOffset, MDLMesh mesh)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            VTXStripGroup strGroup = new VTXStripGroup();
            strGroup.num_vertices  = br.ReadInt32();
            strGroup.vertex_offset = br.ReadInt32();
            strGroup.num_indices = br.ReadInt32();
            strGroup.index_offset = br.ReadInt32();
            strGroup.num_strips = br.ReadInt32();
            strGroup.strip_offset = br.ReadInt32();
            strGroup.strip_group_flags = br.ReadByte();

            // Fill vertex arrays
            List<Vector3> vertexArray = new List<Vector3>();
            List<Vector3> normalArray = new List<Vector3>();
            List<Vector2> texcoordArray = new List<Vector2>();
            List<VertexPositionNormalTextured> verts = new List<VertexPositionNormalTextured>();
            br.BaseStream.Seek(strGroup.vertex_offset + offset, SeekOrigin.Begin);
            for (int i = 0; i < strGroup.num_vertices; i++)
            {
                VTXVertex vertex = ReadVertex(br);
                int vertexID = vertex.orig_mesh_vertex_id + vertexOffset;

                // Get vvd info
                VVDVertex vvdVertex = VVDReader.vertex_buffer[lodNum][vertexID];
                vertexArray.Add(vvdVertex.vertex_position);
                normalArray.Add(vvdVertex.vertex_normal);
                texcoordArray.Add(vvdVertex.vertex_texcoord);
                verts.Add(new VertexPositionNormalTextured(vvdVertex.vertex_position, vvdVertex.vertex_normal, vvdVertex.vertex_texcoord));
            }

            // Fill index array
            br.BaseStream.Seek(offset + strGroup.index_offset, SeekOrigin.Begin);
            List<uint> indexArray = new List<uint>();
            for (int i = 0; i < strGroup.num_indices; i++)
            {
                indexArray.Add((uint)br.ReadUInt16());
            }

            // Create SourceModel
            RenderItem stripitem = new RenderItem(mesh, mesh.material);
            stripitem.nVerts = verts.Count;

            // Set IB
            stripitem.indices = indexArray;
            // Create VB
            stripitem.vb = new HagsVertexBuffer();
            int vertexBytes = verts.Count * VertexPositionNormalTextured.SizeInBytes;
            stripitem.vb.SetVB<VertexPositionNormalTextured>(verts.ToArray(), vertexBytes, VertexPositionNormalTextured.Format, Usage.WriteOnly);
            stripitem.DontOptimize = true;
            stripitem.GenerateIndexBuffer();

            // Process strips
            for (int i = 0; i < strGroup.num_strips; i++)
            {
                RenderItem item = new RenderItem(stripitem, null);
                VTXStrip strip = ReadStrip(br, offset + strGroup.strip_offset + (i* VTXStrip.VTX_STRIP_SIZE), indexArray);
                item.nVerts = strip.num_vertices;
                item.nIndices = strip.num_indices;
                item.IndiceStartIndex = strip.index_offset;
                item.vertexStartIndex = strip.vertex_offset;
                if (strip.strip_flags == 0x02)
                    item.Type = PrimitiveType.TriangleStrip;

                stripitem.items.Add(item);
            }
            mesh.items.Add(stripitem);

            return strGroup;
        }

        public static VTXStrip ReadStrip(BinaryReader br, int offset, List<uint> indexArray)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            VTXStrip strip = new VTXStrip();
            strip.num_indices = br.ReadInt32();
            strip.index_offset = br.ReadInt32();
            strip.num_vertices = br.ReadInt32();
            strip.vertex_offset = br.ReadInt32();
            strip.num_bones = br.ReadInt16();
            strip.strip_flags = br.ReadByte();
            strip.num_bone_state_changes = br.ReadInt32();
            strip.bone_state_change_offset = br.ReadInt32();

            //uint start = indexArray[strip.index_offset];
            //uint end = indexArray[strip.index_offset + strip.num_indices];

            //if ((strip.strip_flags == (byte)VTXStripFlags.STRIP_IS_TRI_LIST))
            //{

            //}
            
            return strip;
        }

        public static VTXVertex ReadVertex(BinaryReader br)
        {
            VTXVertex vertex = new VTXVertex();
            vertex.bone_weight_index = br.ReadBytes(3);
            vertex.num_bones = br.ReadByte();
            vertex.orig_mesh_vertex_id = br.ReadInt16();
            vertex.bone_id = br.ReadChars(3);

            return vertex;
        }

        public static VTXHeader ReadHeader(BinaryReader br)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);
            VTXHeader header = new VTXHeader();

            header.vtx_version = br.ReadInt32();
            header.vertex_cache_size = br.ReadInt32();
            header.max_bones_per_strip = br.ReadUInt16();
            header.max_bones_per_tri = br.ReadUInt16();
            header.max_bones_per_vertex = br.ReadInt32();
            header.check_sum = br.ReadInt32();
            header.num_lods = br.ReadInt32();
            header.mtl_replace_list_offset = br.ReadInt32();
            header.num_body_parts = br.ReadInt32();
            header.body_part_offset = br.ReadInt32();

            return header;
        }
    }
}
