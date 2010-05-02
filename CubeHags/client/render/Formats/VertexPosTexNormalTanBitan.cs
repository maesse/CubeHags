using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;

namespace CubeHags.client
{
    public struct VertexPosTexNormalTanBitan
    {
        public Vector3 Position;
        public Vector2 TextureCoordinate;
        public Vector3 Normal;
        public Vector3 Tangent;
        public Vector3 BiNormal;

        public static int SizeInBytes { get { return sizeof(float) * 14; } }

        public static readonly VertexFormat Format = VertexFormat.Position | VertexFormat.Texture3 | VertexFormat.Normal;

        public VertexPosTexNormalTanBitan(Vector3 Position, Vector2 TextureCoordinate, Vector3 Normal, Vector3 Tangent, Vector3 BiNormal)
        {
            this.Position = Position;
            this.TextureCoordinate = TextureCoordinate;
            this.Normal = Normal;
            this.Tangent = Tangent;
            this.BiNormal = BiNormal;
        }

        public static readonly VertexElement[] Elements = new VertexElement[] {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                new VertexElement(0, sizeof(float)*5, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
                new VertexElement(0, sizeof(float)*8, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Tangent, 0),
                new VertexElement(0, sizeof(float)*11, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Binormal, 0),
                
                
                
                VertexElement.VertexDeclarationEnd
            };
    }
}
