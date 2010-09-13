using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.client.render.Formats;
using CubeHags.client.render;
using SlimDX.Direct3D9;

namespace CubeHags.client.map.Source
{
    public class SourceProp : RenderItem
    {
        public StaticPropLump_t prop_t;
        public SourceModel srcModel;

        public SourceProp(StaticPropLump_t prop_t) : base(null, null)
        {
            this.DontOptimize = true;
            this.prop_t = prop_t;
            Vector3 lightOrigin = Vector3.Zero;
            if ((prop_t.Flags & 2) == 2)
            {
                lightOrigin = prop_t.LightingOrigin;
            }
            else
            {
                lightOrigin = GetIlluminationPoint(srcModel, prop_t.Origin, prop_t.Angles);
            }
            //modelMatrix = Matrix.Translation(prop_t.Origin);
        }

        public new void Render(Effect effect, Device device, bool setMaterial)
        {
            // Render children
            foreach (RenderChildren children in items)
            {
                device.SetStreamSource(0, children.vb.VB, 0, D3DX.GetFVFVertexSize(children.vb.VF));
                device.VertexDeclaration = children.vb.VD;
                device.Indices = children.ib.IB;
                children.Render(effect, device, true);
            }
        }

        public void UpdateMesh()
        {
            // copy mesh from srcModel, and apply lighting, etc..
            if (srcModel == null)
                return;
            items.Clear();
            int leaf = Renderer.Instance.SourceMap.FindLeaf(prop_t.LightingOrigin);
            CompressedLightCube ambient = Renderer.Instance.SourceMap.world.leafs[leaf].ambientLighting;
            Matrix matrix = Matrix.RotationYawPitchRoll(prop_t.Angles.X * (float)(Math.PI / 180f), -prop_t.Angles.Z * (float)(Math.PI / 180f), prop_t.Angles.Y * (float)(Math.PI / 180f)) * Matrix.Translation(prop_t.Origin.X, prop_t.Origin.Y, prop_t.Origin.Z);
            //Renderer.Instance.SourceMap.
            foreach (BodyPart part in srcModel.BodyParts)
            {
                foreach (Model model in part.Models)
                {
                    MDLMesh mesh = model.GetLODMesh(0);
                    foreach (RenderItem item in mesh.items)
                    {
                        // Here we have the vertex data
                        RenderItem child = new RenderItem(this, item.material);
                        VertexPositonNormalColorTexture[] v = new VertexPositonNormalColorTexture[item.verts.Count];
                        item.verts.CopyTo(v);
                        for (int i = 0; i < v.Length; i++)
                        {
                            Vector4 temp =  Vector3.Transform(v[i].Position, matrix);
                            v[i].Position = new Vector3(temp.X, temp.Y, temp.Z);
                            v[i].Normal = Vector3.TransformNormal(v[i].Normal, matrix);
                            Vector3 col;
                            bool inSolid = SourceParser.Instance.ComputeVertexLightFromSpericalSamples(v[i].Position, v[i].Normal, this, out col);
                            
                            // add in ambient light
                            Vector3 normal = v[i].Normal;
                            col += (normal.X * normal.X) * ambient.Color[(normal.X < 0.0f ? 1 : 0)];
                            col += (normal.Y * normal.Y) * ambient.Color[(normal.Y < 0.0f ? 1 : 0) + 2];
                            col += (normal.Z * normal.Z) * ambient.Color[(normal.Z < 0.0f ? 1 : 0) + 4];


                            col *= 255f;

                            // Convert to RGBE8

                            // Determine the largest color component
                            float maxComponent = Math.Max(Math.Max(col.X, col.Y), col.Z);

                            if (maxComponent < 0.000125f)
                            {
                                v[i].Color = new Color4(0.5f, 0f, 0f, 0f).ToArgb();
                                continue;
                            }

                            // Round to the nearest integer exponent
                            float fExp = (float)Math.Ceiling(Math.Log(maxComponent,2));
                            // Divide the components by the shared exponent

                            Vector3 enc = col / (float)(Math.Pow(2, fExp));

                            // Store the shared exponent in the alpha channel
                            float a = ((fExp + 128f) /255f);
                            v[i].Color = new Color4(a, enc.X, enc.Y, enc.Z).ToArgb();
                            
                        }
                        child.verts = new List<VertexPositonNormalColorTexture>(v);
                        child.vb = new HagsVertexBuffer();
                        int vertexBytes = v.Length * VertexPositonNormalColorTexture.SizeInBytes;
                        child.vb.SetVB<VertexPositonNormalColorTexture>(v, vertexBytes, VertexPositonNormalColorTexture.Format, Usage.WriteOnly);
                        
                        child.nVerts = v.Length;
                        child.DontOptimize = true;
                        child.indices = item.indices;
                        child.GenerateIndexBuffer();
                        items.Add(child);
                    }
                }
            }

            int test = 2;
        }

        Matrix AngleMatrix(Vector3 angles)
        {
            Matrix matrix = new Matrix();
            float sr, sp, sy, cr, cp, cy;
            sy = (float)Math.Sin(angles[1] * (Math.PI / 180f));
            cy = (float)Math.Cos(angles[1] * (Math.PI / 180f));
            sp = (float)Math.Sin(angles[0] * (Math.PI / 180f));
            cp = (float)Math.Cos(angles[0] * (Math.PI / 180f));
            sr = (float)Math.Sin(angles[2] * (Math.PI / 180f));
            cr = (float)Math.Cos(angles[2] * (Math.PI / 180f));

            // matrix = (YAW * PITCH) * ROLL
            matrix[0, 0] = cp * cy;
            matrix[1, 0] = cp * sy;
            matrix[2, 0] = -sp;
            matrix[0, 1] = sr * sp * cy + cr * -sy;
            matrix[1, 1] = sr * sp * sy + cr * cy;
            matrix[2, 1] = sr * cp;
            matrix[0, 2] = (cr * sp * cy + -sr * -sy);
            matrix[1, 2] = (cr * sp * sy + -sr * cy);
            matrix[2, 2] = cr * cp;
            matrix[0, 3] = 0.0f;
            matrix[1, 3] = 0.0f;
            matrix[2, 3] = 0.0f;
            return matrix;
        }

        private Vector3 GetIlluminationPoint(SourceModel sourceModel, Vector3 origin, Vector3 angle)
        {
            return origin;
            Matrix matrix = AngleMatrix(angle);
            matrix.set_Columns(3, new Vector4(origin, 1.0f));

        }
    }
}
