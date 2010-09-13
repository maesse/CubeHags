using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.client.render;
using SlimDX.Direct3D9;

namespace CubeHags.client.map.Source
{

    struct MDLHeader
    {
        public const int MDL_MAGIC_NUMBER = (('T' << 24) + ('S' << 16) + ('D' << 8) + 'I');
        public int magic_number;
        public int mdl_version;
        public int check_sum;
        public char[] mdl_name; // 64
        public int mdl_length;

        public Vector3 eye_position;
        public Vector3 illum_position;
        public Vector3 hull_min;
        public Vector3 hull_max;
        public Vector3 view_bbox_min;
        public Vector3 view_bbox_max;

        public int mdl_flags;

        public int num_bones;
        public int bone_offset;

        public int num_bone_controllers;
        public int bone_controller_offset;

        public int num_hitbox_sets;
        public int hitbox_set_offset;

        public int num_local_animations;
        public int local_animation_offset;

        public int num_local_sequences;
        public int local_sequence_offset;

        public int activity_list_version;
        public int events_offseted;

        public int num_textures;
        public int texture_offset;

        public int num_texture_paths;
        public int texture_path_offset;

        public int num_skin_refs;
        public int num_skin_families;
        public int skin_offset;

        public int num_body_parts;
        public int body_part_offset;

        public int num_local_attachments;
        public int local_attachment_offset;

        public int num_local_nodes;
        public int local_node_offset;
        public int local_node_name_offset;

        public int num_flex_desc;
        public int flex_desc_offset;

        public int num_flex_controllers;
        public int flex_controller_offset;

        public int num_flex_rules;
        public int flex_rule_offset;

        public int num_ik_chains;
        public int ik_chain_offset;

        public int num_mouths;
        public int mouth_offset;

        public int num_local_pose_params;
        public int local_pose_param_offset;

        public int surface_prop_offset;

        public int key_value_offset;
        public int key_value_size;

        public int num_local_ik_autoplay_locks;
        public int local_ik_autoplay_lock_offset;

        public float mdl_mass;
        public int mdl_contents;

        public int num_include_models;
        public int include_model_offset;

        public int virtual_model;

        public int anim_block_name_offset;
        public int num_anim_blocks;
        public int anim_block_offset;
        public int anim_block_model;

        public int bone_table_by_name_offset;

        public int vertex_base;
        public int offset_base;

        public byte const_direction_light_dot;
        public byte root_lod;
        public byte[] unused_byte; // 2

        public int zero_frame_cache_offset;

        public int[] unused_fields; // 2
    }

    // bones
    public struct mstudiobone_t
    {
    	public int	sznameindex;
        public int parent;		// parent bone
        public int[] bonecontroller;	// 6 bone controller index, -1 == none
    	// default values
        public Vector3 pos;
        public Quaternion quat;
        public RadianEuler rot;
    	// compression scale
        public Vector3 posscale;
        public Vector3 rotscale;
        public Matrix poseToBone; // 3x4 floats
        public Quaternion qAlignment;
        public int flags;
        public int proctype;
        public int procindex;		// procedural rule
        public int physicsbone;	// index into physically simulated bone
        public int surfacepropidx;	// index into string tablefor property name
        public int contents;		// See BSPFlags.h for the contents flags
        public int[] unused;		// 8 remove as appropriate
    }; // 54 * 4 = 216

    struct MDLTexture
    {
        public int tex_name_offset;
        public int tex_flags;
        public int tex_used;
        public int unused_1;
        public int tex_material;
        public int client_material;
        public int[] unused_array; // 10
    } // 64 bytes

    public class MDLMesh : RenderItem
    {
        public MDLMesh_t mesh;
        //public SourceMaterial material;

        public MDLMesh(MDLMesh_t mesh, RenderChildren parent, SourceMaterial material) : base(parent, material)
        {
            this.mesh = mesh;
        }

        public void Render(Effect effect, Device device, ref int vbID, ref int ibID,ref  int matID)
        {
            // Set material
            //if (setMaterial)
                material.ApplyMaterial(device);

            int primCount = 0;
            switch (Type)
            {
                case PrimitiveType.TriangleList:
                    primCount = nIndices / 3;
                    break;
                case PrimitiveType.TriangleFan:
                case PrimitiveType.TriangleStrip:
                    primCount = nIndices - 2;
                    break;
            }

            // Draw
            //device.DrawIndexedPrimitives(Type, vertexStartIndex, 0, nVerts, IndiceStartIndex, primCount);

            // Render children
            //if (!itemsOptimized || DontOptimize)
            //{
                foreach (RenderChildren children in items)
                {
                    if (children.vb != null && children.vb.VertexBufferID != vbID)
                    {
                        device.SetStreamSource(0, children.vb.VB, 0, D3DX.GetFVFVertexSize(children.vb.VF));
                        //device.VertexDeclaration = children.vb.VD;
                        vbID = children.vb.VertexBufferID;
                    }
                    if (children.ib != null && children.ib.IndexBufferID != ibID)
                    {
                        ibID = children.ib.IndexBufferID;
                        device.Indices = children.ib.IB;
                    }
                    if (children.material != null && children.material.MaterialID != matID)
                    {
                        matID = children.material.MaterialID;
                        children.material.ApplyMaterial(device);
                    }
                    
                    children.Render(effect, device, false);
                }
            //}
        }
    }

    public class Model
    {
        public List<MDLMesh> Meshes;
        public int NumLODs { get { if (Meshes != null) return Meshes.Count; else return 0; } }
        
        public MDLModel MDLModel;
        public int VertexBaseNum { get { return MDLModel.vertex_index / 48; } }

        public Model(MDLModel model)
        {
            this.MDLModel = model;
            Meshes = new List<MDLMesh>();
        }

        public MDLMesh GetLODMesh(int lodNum)
        {
            int lod = (int)Math.Min(lodNum, NumLODs - 1);
            return Meshes[lod];
        }
        
    }

    public class BodyPart
    {
        public MDLBodyPart MDLBodyPart;
        public List<Model> Models;
        public VTXBodyPart VTXBodyPart;

        public BodyPart(MDLBodyPart mdlBodyPart)
        {
            this.MDLBodyPart = mdlBodyPart;
            Models = new List<Model>();
        }
    }

    public class MDLRoot
    {
        public List<BodyPart> BodyParts;

        public MDLRoot()
        {
            BodyParts = new List<BodyPart>();
        }
    }

    public struct MDLBodyPart
    {
        public int mdl_name_index;
        public int num_models;
        public int body_part_base;
        public int model_offset;
    }; // 16bytes

    public struct MDLModel
    {
        public string model_name; // 64
        public int model_type;
        public float bounding_radius;
        public int num_meshes;
        public int mesh_offset;

        public int num_vertices;
        public int vertex_index;
        public int tangents_index;

        public int num_attachments;
        public int attachment_offset;
        public int num_eyeballs;
        public int eyeball_offset;

        public MDLModelVertexData vertex_data;

        public int[] unused_array; // 8
    };

    //public struct mstudioboneweight_t
    //{
    //    float	weight[MAX_NUM_BONES_PER_VERT];
    //    char	bone[MAX_NUM_BONES_PER_VERT]; 
    //    byte	numbones;
    //};

    public struct MDLModelVertexData
    {
        // No useful values are stored in the file for this structure, but we
        // need the size to be right so we can properly read subsequent models
        // from the file
        public int vertex_data_ptr;
        public int tangent_data_ptr;
    };

    public struct MDLMesh_t
    {
        public int  material_index;
        public int  model_index;

        public int  num_vertices;
        public int  vertex_offset;

        public int  num_flexes;
        public int  flex_offset;

        public int  material_type;
        public int  material_param;

        public int  mesh_id;

        public Vector3 mesh_center;
        public MDLMeshVertexData    vertex_data;

        public int[]  unused_array; // 8
    };

    

    public struct MDLMeshVertexData
    {
        public static int MAX_LODS = 8;
        // Used by the Source engine for cache purposes.  This value is allocated
        // in the file, but no meaningful data is stored there
        public int    model_vertex_data_ptr;

        // Indicates the number of vertices used by each LOD of this mesh
        public int[] num_lod_vertices; // AX_LODS
    };

    //
    // VVD
    //
    struct VVDHeader
    {
        public const int VVD_MAGIC_NUMBER = (('V'<<24)+('S'<<16)+('D'<<8)+'I');
        public int    magic_number;
        public int    vvd_version;
        public int    check_sum;

        public int    num_lods;
        public int[]    num_lod_verts; // 8

        public int    num_fixups;
        public int    fixup_table_offset;

        public int    vertex_data_offset;

        public int    tangent_data_offset;
    };

    struct VVDFixupEntry
    {
        public int   lod_number;

        public int   source_vertex_id;
        public int   num_vertices;
    };


    struct VVDBoneWeight
    {
        public float[] weight; // 3
        public char[] bone;// 3
        public byte num_bones; 
    };

    struct VVDVertex
    {
        public VVDBoneWeight   bone_weights;
        public Vector3 vertex_position;
        public Vector3 vertex_normal;
        public Vector2 vertex_texcoord;
    };// 45 + (3 || (3*4)) = 48


    //
    // VTX
    //
    struct VTXHeader
    {
        public int              vtx_version;
        public int              vertex_cache_size;
        public ushort max_bones_per_strip;
        public ushort max_bones_per_tri;
        public int              max_bones_per_vertex;
        public int              check_sum;
        public int              num_lods;
        public int              mtl_replace_list_offset;
        public int              num_body_parts;
        public int              body_part_offset;
    };

    struct VTXMaterialReplacementList
    {
        public int   num_replacements;
        public int   replacement_offset;
    };

    struct VTXMaterialReplacment
    {
        public short material_id;
        public int     replacement_material_name_offset;
    };

    public struct VTXBodyPart
    {
        public int   num_models;
        public int   model_offset;
        public VTXModel[] Models;
    };

    public struct VTXModel
    {
        public int   num_lods;
        public int   lod_offset;
        public VTXModelLOD[] Lods;
    };

    public struct VTXModelLOD
    {
        public int     num_meshes;
        public int     mesh_offset;
        public float switch_point;
        public VTXMesh[] Meshes;
    };

    enum VTXMeshFlags
    {
       MESH_IS_TEETH  = 0x01,
       MESH_IS_EYES   = 0x02
    };

    public struct VTXMesh
    {
        // Can't rely on sizeof() because Valve explicitly packs these structures to
        // 1-byte alignment in the file, which isn't portable
        public const int VTX_MESH_SIZE = 9;
        public int             num_strip_groups;
        public int             strip_group_offset;
        public byte mesh_flags;
        public VTXStripGroup[] StripGroups;
    };

    enum VTXStripGroupFlags
    {
        STRIP_GROUP_IS_FLEXED        = 0x01,
        STRIP_GROUP_IS_HW_SKINNED    = 0x02,
        STRIP_GROUP_IS_DELTA_FLEXED  = 0x04
    };

    public struct VTXStripGroup
    {
        // Can't rely on sizeof() because Valve explicitly packs these structures to
        // 1-byte alignment in the file, which isn't portable
        public const int VTX_STRIP_GROUP_SIZE = 25;
        public int             num_vertices;
        public int             vertex_offset;
        public int             num_indices;
        public int             index_offset;
        public int             num_strips;
        public int             strip_offset;
        public byte strip_group_flags;
        public List<Vector3> Verts;
        public List<Vector3> Normals;
        public List<Vector2> Coords;
        public List<uint> Indices;
        public VTXStrip[] Strips;
    };

    enum VTXStripFlags : byte
    {
        STRIP_IS_TRI_LIST   = 0x01,
        STRIP_IS_TRI_STRIP  = 0x02
    };


    public struct VTXStrip
    {
        // Can't rely on sizeof() because Valve explicitly packs these structures to
        // 1-byte alignment in the .vtx file, which isn't portable
        public const int VTX_STRIP_SIZE = 27;
        public int             num_indices;
        public int             index_offset;
        public int             num_vertices;
        public int             vertex_offset;
        public short num_bones;
        public byte strip_flags;
        public int             num_bone_state_changes;
        public int             bone_state_change_offset;
    };

    struct VTXVertex
    {
        // Can't rely on sizeof() because Valve explicitly packs these structures to
        // 1-byte alignment in the .vtx file, which isn't portable
        public const int VTX_VERTEX_SIZE = 9;
        public byte[] bone_weight_index; // 3
        public byte num_bones;
        public short orig_mesh_vertex_id;
        public char[] bone_id; // 3
    };

    struct VTXBoneStateChange
    {
        public int   hardware_id;
        public int   new_bone_id;
    };
}
