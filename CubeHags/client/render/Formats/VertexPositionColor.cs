using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;

namespace CubeHags.client.render.Formats
{
    public struct VertexPositionColor
    {
        public Vector3 Position;
        public int Color;


        public static int SizeInBytes { get { return sizeof(int) + sizeof(float) * 3; } }
        
        // public VertexFormat Format { get { return _Format; } }
        public static readonly VertexFormat Format = VertexFormat.Position | VertexFormat.Diffuse;

        public VertexPositionColor(Vector3 Position, Color4 Color)
        {
            this.Position = Position;
            this.Color = Color.ToArgb();
            
        }

        public static VertexElement[] Elements = new VertexElement[] {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Color, DeclarationMethod.Default, DeclarationUsage.Color, 0),
                VertexElement.VertexDeclarationEnd
            };


    }
}
