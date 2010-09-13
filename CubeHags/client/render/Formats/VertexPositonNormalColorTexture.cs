using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;

namespace CubeHags.client.render.Formats
{
    public struct VertexPositonNormalColorTexture
    {
        public Vector3 Position;
        public Vector3 Normal;
        public int Color;
        public Vector2 texcoords;

        public static int SizeInBytes { get { return sizeof(float) * 9; } }

        // public VertexFormat Format { get { return _Format; } }
        public static readonly VertexFormat Format = VertexFormat.Position | VertexFormat.Texture1 | VertexFormat.Normal | VertexFormat.Diffuse;

        public VertexPositonNormalColorTexture(Vector3 Position, Vector3 normal, Color4 Color, Vector2 texcoords)
        {
            this.Position = Position;
            this.Color = Color.ToArgb();
            this.texcoords = texcoords;
            this.Normal = normal;
        }

        public static VertexElement[] Elements = new VertexElement[] {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
                new VertexElement(0, sizeof(float)*6, DeclarationType.Color, DeclarationMethod.Default, DeclarationUsage.Color, 0),
                new VertexElement(0, sizeof(float)*7, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                VertexElement.VertexDeclarationEnd
            };
    }
}
