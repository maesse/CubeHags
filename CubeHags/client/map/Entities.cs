using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.client.map.Source;
using SlimDX;

namespace CubeHags.client.map
{
    public class FogController : Entity
    {
        public bool Enabled = false;
        public float FogStart;
        public float FogEnd;
        public float MaxDensity;
        public int FarZ;
        public Color3 FogColor;
        public bool FogBlend = false;
        public Color3 FogColor2;
        public Vector3 FogDirection = Vector3.Zero;

        public FogController(Dictionary<string, string> Values)
            : base(Values)
        {

        }

        public void Init()
        {
            if (Values.ContainsKey("fogenable"))
                bool.TryParse(Values["fogenable"], out Enabled);

            if (Values.ContainsKey("fogblend"))
                bool.TryParse(Values["fogblend"], out FogBlend);
            try
            {
                if (Values.ContainsKey("fogcolor"))
                {
                
                    string[] split = Values["fogcolor"].Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    int[] pars = new int[] { int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]) };
                    FogColor = new Color3(pars[2], pars[1], pars[0]);
                }

                if (Values.ContainsKey("fogstart"))
                    FogStart = float.Parse(Values["fogstart"], System.Globalization.CultureInfo.InvariantCulture);
                if (Values.ContainsKey("fogend"))
                    FogEnd = float.Parse(Values["fogend"], System.Globalization.CultureInfo.InvariantCulture);
                if (Values.ContainsKey("fogmaxdensity"))
                    MaxDensity = float.Parse(Values["fogmaxdensity"], System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
            }
        }

        public Color3 GetFogColor()
        {
            return FogColor;
        }
    }
}
