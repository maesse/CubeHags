using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;
using CubeHags.client.map.Source;

namespace CubeHags.client.render.Formats
{
    public struct VertexPropInstance
    {
        public Vector4 row1;
        public Vector4 row2;
        public Vector4 row3;
        public Vector3 cubex;
        public Vector3 cubex1;
        public Vector3 cubey;
        public Vector3 cubey1;
        public Vector3 cubez;
        public Vector3 cubez1;

        public static int SizeInBytes { get { return sizeof(float) * 30; } }
        public static readonly VertexFormat Format = VertexFormat.Texture6 | VertexFormat.Position;

        public VertexPropInstance(Matrix matrix, CompressedLightCube light)
        {
            row1 = matrix.get_Rows(0);
            row2 = matrix.get_Rows(1);
            row3 = matrix.get_Rows(2);

            Vector4 vec = matrix.get_Rows(3);
            row1.W = vec.X;
            row2.W = vec.Y;
            row3.W = vec.Z;

            cubex = light.Color[0];
            cubex1 = light.Color[1];
            cubey = light.Color[2];
            cubey1 = light.Color[3];
            cubez = light.Color[4];
            cubez1 = light.Color[5];
        }

        public static VertexElement[] Elements = new VertexElement[] {
                new VertexElement(1, 0, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 1),
                new VertexElement(1, sizeof(float)*4, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 2),
                new VertexElement(1, sizeof(float)*8, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 3),
                new VertexElement(1, sizeof(float)*12, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 1),
                new VertexElement(1, sizeof(float)*15, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 2),
                new VertexElement(1, sizeof(float)*18, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 3),
                new VertexElement(1, sizeof(float)*21, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 4),
                new VertexElement(1, sizeof(float)*24, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 5),
                new VertexElement(1, sizeof(float)*27, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 6),
                VertexElement.VertexDeclarationEnd
            };
    }

    public struct VertexPosNorInstance
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;
        public Vector4 InstPosition;
        public Vector4 InstRotation;
        public Vector4 InstScale;
        public Vector3 cubex;
        public Vector3 cubex1;
        public Vector3 cubey;
        public Vector3 cubey1;
        public Vector3 cubez;
        public Vector3 cubez1;

        public static int SizeInBytes { get { return sizeof(float) * 38; } }

        public static readonly VertexFormat Format = VertexFormat.Position | VertexFormat.Normal | VertexFormat.Texture7;


        //public VertexPosNorInstance()
        //{
        //    this.Position = Vector3.Zero;
        //    this.InstPosition = Vector3.Zero;
        //    this.InstRotation = Vector3.Zero;
        //    this.Normal = Vector3.Zero;
        //    this.TextureCoordinate = Vector2.Zero;
        //}

        public static VertexElement[] Elements = new VertexElement[] {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
                new VertexElement(0, sizeof(float)*6, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                new VertexElement(1, 0, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 1),
                new VertexElement(1, sizeof(float)*4, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 2),
                new VertexElement(1, sizeof(float)*8, DeclarationType.Float4, DeclarationMethod.Default, DeclarationUsage.Position, 3),
                new VertexElement(1, sizeof(float)*12, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 1),
                new VertexElement(1, sizeof(float)*15, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 2),
                new VertexElement(1, sizeof(float)*18, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 3),
                new VertexElement(1, sizeof(float)*21, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 4),
                new VertexElement(1, sizeof(float)*24, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 5),
                new VertexElement(1, sizeof(float)*27, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 6),
                VertexElement.VertexDeclarationEnd
            };
        public static VertexDeclaration vd { get { if (_vd == null) _vd = new VertexDeclaration(Renderer.Instance.device, Elements); return _vd; } }
        public static VertexDeclaration _vd = null;
    }
}
