using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;

namespace CubeHags.client.map.Source
{
    public struct VertexPosTex
    {
        public Vector3 Position;
        public Vector2 TextureCoordinate;

        public static int SizeInBytes { get { return sizeof(float) * 5; } }

        public static readonly VertexFormat Format = VertexFormat.Position | VertexFormat.Texture1;

        public VertexPosTex(Vector3 Position, Vector2 TextureCoordinate)
        {
            this.Position = Position;
            this.TextureCoordinate = TextureCoordinate;
        }

    //    public VertexPosTex(Vector3 Position, float u, float v)
    //    {
    //        this.Position = Position;
    //        this.TextureCoordinate = new Vector2(u,v);
    //    }

    //    public VertexPosTex(float x, float y, float z, float u, float v)
    //    {
    //        this.Position = new Vector3(x,y,z);
    //        this.TextureCoordinate = new Vector2(u, v);
    //    }

        public static readonly VertexElement[] Elements = new VertexElement[] {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                VertexElement.VertexDeclarationEnd
            };
    }
}
