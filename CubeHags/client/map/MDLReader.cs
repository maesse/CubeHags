using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using System.IO;
using CubeHags.client.common;

namespace CubeHags.client.map.Source
{
    class MDLReader
    {









        public static SourceModel srcModel;
        public static SourceMaterial[] materials;

        public static bool ReadFile(string filename)
        {
            // Check filename and if file exists
            if (!filename.EndsWith(".mdl") || !Path.HasExtension(filename))
            {
                System.Console.WriteLine("[MDLReader] Filename not accepted. (file:{0})", filename);
                return false;
            }

            FCFile file = FileCache.Instance.GetFile(filename);
            if (file == null)
            {
                System.Console.WriteLine("[MDLReader] MDL file not found. (file:{0})", filename);
                return false;
            }

            srcModel = new SourceModel();
            
            

            using (FileStream stream = File.OpenRead(file.FullName))
            {
                // Read header
                BinaryReader br = new BinaryReader(stream);
                MDLHeader header = ReadHeader(br);

                // Check magic number
                if (header.magic_number != MDLHeader.MDL_MAGIC_NUMBER)
                {
                    System.Console.WriteLine("[MDLReader] Bad Magic number (file:{0})", filename);
                    return false;
                }

                // Read texture paths
                string[] texturePaths = new string[header.num_texture_paths];
                StringBuilder strBuilder = new StringBuilder(256);
                for (int i = 0; i < header.num_texture_paths; i++)
                {
                    // Seek to offset index
                    int indexoffset = header.texture_path_offset + (i * sizeof(int));
                    br.BaseStream.Seek(indexoffset, SeekOrigin.Begin);
                    // Get offset for this texture path
                    int texoffset = br.ReadInt32();
                    br.BaseStream.Seek(texoffset, SeekOrigin.Begin);

                    // Read name until \0 char is met
                    char curChar;
                    while((curChar = br.ReadChar()) != '\0' && strBuilder.Length < 256) 
                    {
                        strBuilder.Append(curChar);
                    }
                    texturePaths[i] = strBuilder.ToString();
                    strBuilder.Length = 0;
                }

                // Read MDLTextures
                string[] materialNames = new string[header.num_textures];
                MDLTexture[] mdltexs = new MDLTexture[header.num_textures];
                materials = new SourceMaterial[header.num_textures];
                for (int i = 0; i < header.num_textures; i++)
                {
                    // Seek to offset index
                    int indexoffset = header.texture_offset + (i * 64); // MDLTexture is 64b
                    MDLTexture mdltex = ReadMDLTexture(br, indexoffset);

                    // Get material name
                    br.BaseStream.Seek(indexoffset + mdltex.tex_name_offset, SeekOrigin.Begin);

                    // Read name until \0 char is met
                    char curChar;
                    while ((curChar = br.ReadChar()) != '\0' && strBuilder.Length < 256)
                    {
                        strBuilder.Append(curChar);
                    }
                    mdltexs[i] = mdltex;
                    materialNames[i] = strBuilder.ToString();
                    strBuilder.Length = 0;

                    // Load material
                    SourceMaterial material = TextureManager.Instance.LoadMaterial(materialNames[i]);
                    materials[i] = material;
                }

                
                // Process the main models bodyparts
                for (int i = 0; i < header.num_body_parts; i++)
                {
                    int offset = header.body_part_offset + (i * 16); // MDLBodyPart size = 16bytes
                    BodyPart bp = ReadBodyPart(br, offset);
                    srcModel.BodyParts.Add(bp);
                }

                // Read bones
                for (int i = 0; i < header.num_bones; i++)
                {
                    int offset = header.bone_offset + (i * 216);
                    mstudiobone_t bone = ReadBone(br, offset);
                }
            }
            
            // Read VVD (Vertex Data) datafile
            FCFile vvdFile = FileCache.Instance.GetFile(FCFile.GetFileName(file.FileName, false) + ".vvd");
            if (vvdFile != null)
            {
                VVDReader.ReadFile(vvdFile.FullName);
            }

            // Read VTX (index & primitive data) file
            FCFile vtxFile = FileCache.Instance.GetFile(FCFile.GetFileName(file.FileName, false) + ".dx90.vtx");
            if (vtxFile != null)
            {
                VTXReader.ReadFile(vtxFile.FullName, srcModel);
            }

            return true;
        }

        private static mstudiobone_t ReadBone(BinaryReader br, int offset)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            mstudiobone_t bone = new mstudiobone_t();
            bone.sznameindex = br.ReadInt32();

            bone.parent = br.ReadInt32();
            bone.bonecontroller = new int[6];
            for (int i = 0; i < 6; i++)
			{
                bone.bonecontroller[i] = br.ReadInt32();
			}
            bone.pos = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
            bone.quat = new Quaternion(br.ReadSingle(),br.ReadSingle(),br.ReadSingle(),br.ReadSingle());
            bone.rot = new RadianEuler() { vec = new Vector3(br.ReadSingle(),br.ReadSingle(),br.ReadSingle()) } ;
            bone.posscale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            bone.rotscale = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            bone.poseToBone = new Matrix();
            bone.poseToBone.set_Rows(0, new Vector4(br.ReadSingle(),br.ReadSingle(),br.ReadSingle(),br.ReadSingle()));
            bone.poseToBone.set_Rows(1, new Vector4(br.ReadSingle(),br.ReadSingle(),br.ReadSingle(),br.ReadSingle()));
            bone.poseToBone.set_Rows(2, new Vector4(br.ReadSingle(),br.ReadSingle(),br.ReadSingle(),br.ReadSingle()));
            bone.qAlignment = new Quaternion(br.ReadSingle(), br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            bone.flags = br.ReadInt32();
            bone.proctype = br.ReadInt32();
            bone.procindex = br.ReadInt32();
            bone.physicsbone = br.ReadInt32();
            bone.surfacepropidx = br.ReadInt32();
            bone.contents = br.ReadInt32();
            bone.unused = new int[8];
            for (int i = 0; i < 8; i++)
            {
                bone.unused[i] = br.ReadInt32();
            }
            br.BaseStream.Seek(offset + bone.sznameindex, SeekOrigin.Begin);
            StringBuilder str = new StringBuilder(64);
            char c;
            while((c = br.ReadChar()) != '\0' && str.Length < 64) {
                str.Append(c);
            }
            string name = str.ToString();
            return bone;
        }

        private static BodyPart ReadBodyPart(BinaryReader br, int offset)
        {
            // Read bodypart struct
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            MDLBodyPart mdlbp = new MDLBodyPart();
            mdlbp.mdl_name_index = br.ReadInt32();
            mdlbp.num_models = br.ReadInt32();
            mdlbp.body_part_base = br.ReadInt32();
            mdlbp.model_offset = br.ReadInt32();

            BodyPart bodypart = new BodyPart(mdlbp);
            // Process bodyparts the models
            if (mdlbp.num_models > 1)
            {
                int test = 2;
            }
            for (int i = 0; i < mdlbp.num_models; i++)
            {
                Model model = ReadModel(br, offset + mdlbp.model_offset + (i * 120)); // 120 bytes for MDLModel
                bodypart.Models.Add(model);
            }

            return bodypart;
        }

        private static MDLTexture ReadMDLTexture(BinaryReader br, int offset)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            MDLTexture tex = new MDLTexture();

            tex.tex_name_offset = br.ReadInt32();
            tex.tex_flags = br.ReadInt32();
            tex.tex_used = br.ReadInt32();
            tex.unused_1 = br.ReadInt32();
            tex.tex_material = br.ReadInt32();
            tex.client_material = br.ReadInt32();
            tex.unused_array = new int[10]; // 10
            for (int i = 0; i < 10; i++)
            {
                tex.unused_array[i] = br.ReadInt32();
            }

            return tex;
        }

        private static Model ReadModel(BinaryReader br, int offset)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);

       
            MDLModel mdl = new MDLModel();
            string model_name = ASCIIEncoding.ASCII.GetString(br.ReadBytes(64));
            int nullindex = model_name.IndexOf('\0');
            if (nullindex != -1)
                model_name = model_name.Substring(0, nullindex);
            mdl.model_name = model_name;
            mdl.model_type = br.ReadInt32();
            mdl.bounding_radius = br.ReadSingle();
            mdl.num_meshes= br.ReadInt32();
            mdl.mesh_offset= br.ReadInt32();
            mdl.num_vertices= br.ReadInt32();
            mdl.vertex_index= br.ReadInt32();
            mdl.tangents_index= br.ReadInt32();
            mdl.num_attachments= br.ReadInt32();
            mdl.attachment_offset= br.ReadInt32();
            mdl.num_eyeballs= br.ReadInt32();
            mdl.eyeball_offset= br.ReadInt32();
            mdl.vertex_data = new MDLModelVertexData();
            mdl.vertex_data.vertex_data_ptr = br.ReadInt32();
            mdl.vertex_data.tangent_data_ptr = br.ReadInt32();

            mdl.unused_array = new int[8]; // 8
            for (int i = 0; i < 8; i++)
            {
                mdl.unused_array[i] = br.ReadInt32();
            }

            Model model = new Model(mdl);
            if (mdl.num_meshes >1)
            {
                int test = 2;
            }
            for (int i = 0; i < mdl.num_meshes; i++)
            {
                MDLMesh mesh = ReadMesh(br, offset + mdl.mesh_offset + (i * 116)); // 88 bytes for MDLMesh

                model.Meshes.Add(mesh);
            }
            if (mdl.num_meshes > 1)
            {
                int test = 2;
            }
            return model;
    
        }

        private static MDLMesh ReadMesh(BinaryReader br, int offset)
        {
            br.BaseStream.Seek(offset, SeekOrigin.Begin);
            MDLMesh_t mesh_t = new MDLMesh_t();
            mesh_t.material_index = br.ReadInt32();
            mesh_t.model_index = br.ReadInt32();

            mesh_t.num_vertices = br.ReadInt32();
            mesh_t.vertex_offset = br.ReadInt32();

            mesh_t.num_flexes = br.ReadInt32();
            mesh_t.flex_offset = br.ReadInt32();

            mesh_t.material_type = br.ReadInt32();
            mesh_t.material_param = br.ReadInt32();

            mesh_t.mesh_id = br.ReadInt32();

            mesh_t.mesh_center = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            mesh_t.vertex_data = new MDLMeshVertexData();
            mesh_t.vertex_data.model_vertex_data_ptr = br.ReadInt32();
            mesh_t.vertex_data.num_lod_vertices = new int[8];
            for (int i = 0; i < 8; i++)
			{
                mesh_t.vertex_data.num_lod_vertices[i] = br.ReadInt32();
			}

            mesh_t.unused_array = new int[8]; // 8
            for (int i = 0; i < 8; i++)
			{
                mesh_t.unused_array[i] = br.ReadInt32();
			}

            MDLMesh mesh = new MDLMesh(mesh_t, srcModel, materials[mesh_t.material_index]);
            return mesh;
        }

        private static MDLHeader ReadHeader(BinaryReader br)
        {
            br.BaseStream.Seek(0, SeekOrigin.Begin);
            MDLHeader header = new MDLHeader();
            header.magic_number = br.ReadInt32();
            header.mdl_version = br.ReadInt32();
            header.check_sum = br.ReadInt32();
            header.mdl_name = br.ReadChars(64); // 64
            header.mdl_length = br.ReadInt32();

            header.eye_position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            header.illum_position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            header.hull_min = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            header.hull_max = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            header.view_bbox_min = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            header.view_bbox_max = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());

            header.mdl_flags = br.ReadInt32();

            header.num_bones = br.ReadInt32();
            header.bone_offset = br.ReadInt32();

            header.num_bone_controllers = br.ReadInt32();
            header.bone_controller_offset = br.ReadInt32();

            header.num_hitbox_sets = br.ReadInt32();
            header.hitbox_set_offset = br.ReadInt32();

            header.num_local_animations = br.ReadInt32();
            header.local_animation_offset = br.ReadInt32();

            header.num_local_sequences = br.ReadInt32();
            header.local_sequence_offset = br.ReadInt32();

            header.activity_list_version = br.ReadInt32();
            header.events_offseted = br.ReadInt32();

            header.num_textures = br.ReadInt32();
            header.texture_offset = br.ReadInt32();

            header.num_texture_paths = br.ReadInt32();
            header.texture_path_offset = br.ReadInt32();

            header.num_skin_refs = br.ReadInt32();
            header.num_skin_families = br.ReadInt32();
            header.skin_offset = br.ReadInt32();

            header.num_body_parts = br.ReadInt32();
            header.body_part_offset = br.ReadInt32();

            header.num_local_attachments = br.ReadInt32();
            header.local_attachment_offset = br.ReadInt32();

            header.num_local_nodes = br.ReadInt32();
            header.local_node_offset = br.ReadInt32();
            header.local_node_name_offset = br.ReadInt32();

            header.num_flex_desc = br.ReadInt32();
            header.flex_desc_offset = br.ReadInt32();

            header.num_flex_controllers = br.ReadInt32();
            header.flex_controller_offset = br.ReadInt32();

            header.num_flex_rules = br.ReadInt32();
            header.flex_rule_offset = br.ReadInt32();

            header.num_ik_chains = br.ReadInt32();
            header.ik_chain_offset = br.ReadInt32();

            header.num_mouths = br.ReadInt32();
            header.mouth_offset = br.ReadInt32();

            header.num_local_pose_params = br.ReadInt32();
            header.local_pose_param_offset = br.ReadInt32();

            header.surface_prop_offset = br.ReadInt32();

            header.key_value_offset = br.ReadInt32();
            header.key_value_size = br.ReadInt32();

            header.num_local_ik_autoplay_locks = br.ReadInt32();
            header.local_ik_autoplay_lock_offset = br.ReadInt32();

            header.mdl_mass = br.ReadSingle();
            header.mdl_contents = br.ReadInt32();

            header.num_include_models = br.ReadInt32();
            header.include_model_offset = br.ReadInt32();

            // Originally a mutable void * (changed for portability)
            header.virtual_model = br.ReadInt32();

            header.anim_block_name_offset = br.ReadInt32();
            header.num_anim_blocks = br.ReadInt32();
            header.anim_block_offset = br.ReadInt32();

            // Originally a mutable void * (changed for portability)
            header.anim_block_model = br.ReadInt32();

            header.bone_table_by_name_offset = br.ReadInt32();

            // Originally both void * (changed for portability)
            header.vertex_base = br.ReadInt32();
            header.offset_base = br.ReadInt32();

            header.const_direction_light_dot = br.ReadByte();
            header.root_lod = br.ReadByte();
            header.unused_byte = br.ReadBytes(2); // 2

            header.zero_frame_cache_offset = br.ReadInt32();

            header.unused_fields = new int[] { br.ReadInt32(), br.ReadInt32() }; // 2

            return header;
        }
    }
}
