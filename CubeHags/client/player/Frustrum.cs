using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.common;

namespace CubeHags.client.player
{
    public class Frustrum
    {
        //public struct cplane_t
        //{
        //    public Vector3	normal;
        //    public float dist;
        //    public byte type;			// for fast side tests
        //    public byte signbits;		// signx + (signy<<1) + (signz<<1)
        //}

        
        
        public static double AngToRad = Math.PI / 180f;
        cplane_t[] frustum = new cplane_t[4];
        Vector3[] axis = new Vector3[3];

        public Frustrum()
        {
            for (int i = 0; i < 4; i++)
            {
                frustum[i] = new cplane_t();
            }
            for (int i = 0; i < 3; i++)
            {
                axis[i] = Vector3.Zero;
            }
        }


        // Updates frustrum planes for this frame
        public void SetupFrustrum(Camera camera)
        {
            
        }

        // Checks if a box is culled by the view frustum
        public bool CullBox(Vector3 mins, Vector3 maxs)
        {
            bool result = true;
            for (int i = 0; i < 4; i++)
            {


                if (Common.Instance.BoxOnPlaneSide(ref mins, ref maxs, frustum[i]) != 2)
                    result = false;

            }
            return result;
        }


        

        

        private Vector3 RotatePointAroundVector(Vector3 dir, Vector3 point, float degrees)
        {
            degrees = (float)(AngToRad * degrees);
            float sind = (float)Math.Sin(degrees);
            float cosd = (float)Math.Cos(degrees);
            float expr = (float)(1 - cosd) * Vector3.Dot(dir, point);
            Vector3 dxp = Vector3.Cross(dir, point);

            Vector3 dst = new Vector3(expr * dir[0] + cosd * point[0] + sind * dxp[0],
                                        expr * dir[1] + cosd * point[1] + sind * dxp[1],
                                        expr * dir[2] + cosd * point[2] + sind * dxp[2]);

            return dst;
        }

        
    }
}
