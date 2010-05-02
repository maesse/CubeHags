using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;

namespace CubeHags.client.render.Formats
{
    public struct VertexPositionNormalTexturedLightmap
    {
        public static VertexFormat Format = VertexFormat.Position | VertexFormat.Normal | VertexFormat.Texture2;

        public Vector3 position;
        public Vector3 normal;
        public Vector2 textureCoords;
        public Vector2 lightmapCoords;

        public static int SizeInBytes = sizeof(float) * 10;

        public VertexPositionNormalTexturedLightmap(Vector3 position, Vector3 normal, Vector2 textureCoords, Vector2 lightmapCoords)
        {
            this.position = position;
            this.normal = normal;
            this.textureCoords = textureCoords;
            this.lightmapCoords = lightmapCoords;

        }

        public static VertexElement[] VertexElements = new VertexElement[]
            {
                new VertexElement(0, 0, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Position, 0),
                new VertexElement(0, sizeof(float)*3, DeclarationType.Float3, DeclarationMethod.Default, DeclarationUsage.Normal, 0),
                new VertexElement(0, sizeof(float)*6, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 0),
                new VertexElement(0, sizeof(float)*8, DeclarationType.Float2, DeclarationMethod.Default, DeclarationUsage.TextureCoordinate, 1),
                VertexElement.VertexDeclarationEnd
            };
    }
}
