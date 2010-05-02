using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;

namespace CubeHags.client.render.Formats
{
    public struct VertexPositionNormalTextured
    {
        public Vector3 Position;
        public Vector3 Normal;
        public Vector2 TextureCoordinate;

        public static int SizeInBytes { get { return sizeof(float) * 8; } }

       // public VertexFormat Format { get { return _Format; } }
        public static readonly VertexFormat Format = VertexFormat.Position | VertexFormat.Normal | VertexFormat.Texture1;

        public VertexPositionNormalTextured(Vector3 Position, Vector3 Normal, Vector2 TextureCoordinate)
        {
            this.Position = Position;
            this.Normal = Normal;
            this.TextureCoordinate = TextureCoordinate;
        }

        public VertexPositionNormalTextured(Vector3 Position, Vector2 TextureCoordinate)
        {
            this.Position = Position;
            this.Normal = Vector3.Zero;
            this.TextureCoordinate = TextureCoordinate;
        }

        public VertexPositionNormalTextured(float x, float y, float z, float u, float v)
        {
            this.Position = new Vector3(x, y, z);
            this.Normal = Vector3.Zero;
            this.TextureCoordinate = new Vector2(u, v);
        }

        //public VertexPositionNormalTextured(Vector3 Position, float u, float v)
        //{
        //    this.Position = Position;
        //    this.TextureCoordinate = new Vector2(u, v);
        //}

        //public VertexPositionNormalTextured(float x, float y, float z, float u, float v)
        //{
        //    this.Position = new Vector3(x, y, z);
        //    this.TextureCoordinate = new Vector2(u, v);
        //}

        public static VertexElement[] Elements = new VertexElement[] {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
                new VertexElement(0, sizeof(float)*6, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                VertexElement.VertexDeclarationEnd
            };


    }
}
