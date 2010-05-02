using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using CubeHags.client.map.Source;
using CubeHags.client.render.Formats;
using SlimDX;

namespace CubeHags.client.render
{
    // Groups instances of one model together in one RenderItem
    public class InstanceItem : RenderItem
    {
        public List<BodyPart> Parts = new List<BodyPart>();
        public int nInstances = 0;
        private VertexDeclaration vertexDecl;
        public int InstanceBufferOffset; // for shared instance buffer
        //public List<float> lightcubearr = new List<float>();

        public InstanceItem()
            : base(null, null)
        {
            vertexDecl = VertexPosNorInstance.vd;
        }

        public new void Render(Effect effect, Device device, bool setMaterial)
        {
            
            int vbID = 0, ibID = 0, matID = 0;
            device.VertexDeclaration = vertexDecl;
            
            device.SetStreamSourceFrequency(0, nInstances, StreamSource.IndexedData);

            //if (lightcubearr.Count <= 63 * 6 * 3 && lightcubearr.Count > 0)
            //    Renderer.Instance.effect.SetValue<float>("ambientLight", lightcubearr.ToArray());
            //Renderer.Instance.effect.CommitChanges();

            // Set InstanceBuffer
            device.SetStreamSource(1, vb.VB, InstanceBufferOffset, D3DX.GetDeclarationVertexSize(VertexPropInstance.Elements, 1));
            device.SetStreamSourceFrequency(1,1,StreamSource.InstanceData);

            // Render Bodyparts
            foreach (BodyPart part in Parts)
            {
                foreach (Model model in part.Models)
                {
                    MDLMesh mesh = model.GetLODMesh(0);
                    mesh.Render(effect, device, ref vbID, ref ibID, ref matID);
                }
            }
            
        }
    }
}
