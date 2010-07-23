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
            Vector3 lightOrigin = Vector3.Zero;
            if ((prop_t.Flags & 2) == 2)
            {
                lightOrigin = prop_t.LightingOrigin;
            }
            else
            {
                lightOrigin = GetIlluminationPoint(srcModel, prop_t.Origin, prop_t.Angles);
            }
            //modelMatrix = Matrix.Translation(prop_t.Origin);
        }

        Matrix AngleMatrix(Vector3 angles)
        {
            Matrix matrix = new Matrix();
            float sr, sp, sy, cr, cp, cy;
            sy = (float)Math.Sin(angles[1] * (Math.PI / 180f));
            cy = (float)Math.Cos(angles[1] * (Math.PI / 180f));
            sp = (float)Math.Sin(angles[0] * (Math.PI / 180f));
            cp = (float)Math.Cos(angles[0] * (Math.PI / 180f));
            sr = (float)Math.Sin(angles[2] * (Math.PI / 180f));
            cr = (float)Math.Cos(angles[2] * (Math.PI / 180f));

            // matrix = (YAW * PITCH) * ROLL
            matrix[0, 0] = cp * cy;
            matrix[1, 0] = cp * sy;
            matrix[2, 0] = -sp;
            matrix[0, 1] = sr * sp * cy + cr * -sy;
            matrix[1, 1] = sr * sp * sy + cr * cy;
            matrix[2, 1] = sr * cp;
            matrix[0, 2] = (cr * sp * cy + -sr * -sy);
            matrix[1, 2] = (cr * sp * sy + -sr * cy);
            matrix[2, 2] = cr * cp;
            matrix[0, 3] = 0.0f;
            matrix[1, 3] = 0.0f;
            matrix[2, 3] = 0.0f;
            return matrix;
        }

        private Vector3 GetIlluminationPoint(SourceModel sourceModel, Vector3 origin, Vector3 angle)
        {
            return origin;
            Matrix matrix = AngleMatrix(angle);
            matrix.set_Columns(3, new Vector4(origin, 1.0f));

        }
    }
}
