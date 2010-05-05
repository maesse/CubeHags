using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;

namespace CubeHags.client.map.Source
{
    public class SourceProp
    {
        public StaticPropLump_t prop_t;
        public SourceModel srcModel;

        public SourceProp(StaticPropLump_t prop_t)
        {
            this.prop_t = prop_t;
            //modelMatrix = Matrix.Translation(prop_t.Origin);
        }

    }
}
