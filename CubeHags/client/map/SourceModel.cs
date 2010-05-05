using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;

namespace CubeHags.client.map.Source
{
    public class SourceModel : RenderItem
    {
        public List<BodyPart> BodyParts = new List<BodyPart>();
        public int LODLevel = 0;
        public string Name;

        public SourceModel()
            : base(null, null)
        {
        }
    }
}
