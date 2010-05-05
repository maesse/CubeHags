using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.map.Source;
using SlimDX.Direct3D9;

namespace CubeHags.client.render
{
    // Represents a group of RenderItems' indices that are grouped up in an IB to be rendered in one call.
    // This is a Struct because these objects will be created many times pr. frame
    public struct DynamicItem
    {
        public HagsIndexBuffer ib;
        public HagsVertexBuffer vb;
        public SourceMaterial material;

        public PrimitiveType Type;

        public int nVerts; // nVerts to be used in this call (max-min)
        public int vertexStartIndex; // Start offset for VB
        public int lowestIndiceValue;

        public int nIndices; // nIndices to use from IB
        public int IndiceStartIndex; // Start offset for IB

        public void Render(Effect effect, Device device, bool setMaterial)
        {
            // Set material
            if (setMaterial)
                material.ApplyMaterial(device);

            // Calculate number of primitives to draw.
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
            if (primCount > 0)
            {
                device.DrawIndexedPrimitives(Type, vertexStartIndex, lowestIndiceValue, nVerts, IndiceStartIndex, primCount);
            }
        }
    }
}
