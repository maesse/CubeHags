using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using CubeHags.client.map.Source;

namespace CubeHags.client.render
{
    public class ViewParams
    {
        public Orientation Origin;
        public Orientation World;
        public Vector3 PVSOrigin;
        public int FrameSceneNum;
        public int FrameCount;
        public int viewportX, viewportY, viewportWidth, viewportHeight;
        public float fovX, fovY;
        public Matrix ProjectionMatrix = Matrix.Identity;
        public cplane_t[] frustum = new cplane_t[4];
        public Vector3[] visBounds = new Vector3[2];
        public float zFar;

        public Vector3 viewangles;
        public Vector3 vieworg;
        public Vector3[] viewaxis;
        public float time;

        public ViewParams()
        {
            for (int i = 0; i < 4; i++)
            {
                frustum[i] = new cplane_t();
            }
        }

        public void SetupProjection(float zProj, bool computeFrustum)
        {
            float ymax = (float)(zProj * Math.Tan(fovY * Math.PI / 360.0f));
            float ymin = -ymax;
            float xmax = (float)(zProj * Math.Tan(fovX * Math.PI / 360.0f));
            float xmin = -xmax;

            float width = xmax - xmin;
            float height = ymax - ymin;

            ProjectionMatrix[0,0] = 2 * zProj / width;
            ProjectionMatrix[1,0] = 0;
            ProjectionMatrix[2,0] = (xmax + xmin + 2 * 0f) / width;
            ProjectionMatrix[3,0] = 2 * zProj * 0f / width;

            ProjectionMatrix[0,1] = 0;
            ProjectionMatrix[1,1] = 2 * zProj / height;
            ProjectionMatrix[2,1] = (ymax + ymin) / height;	// normally 0
            ProjectionMatrix[3,1] = 0;

            ProjectionMatrix[0,3] = 0;
            ProjectionMatrix[1,3] = 0;
            ProjectionMatrix[2,3] = -1;
            ProjectionMatrix[3,3] = 0;

            SetupFrustum(xmin, xmax, ymax, zProj);
        }

        public void SetupProjectionZ()
        {
            float zNear, zFar, depth;

            zNear = 1.0f;
            zFar = 80000f;
            depth = zFar - zNear;

            ProjectionMatrix[0,2] = 0;
            ProjectionMatrix[1,2] = 0;
            ProjectionMatrix[2,2] = -(zFar + zNear) / depth;
            ProjectionMatrix[3,2] = -2 * zFar * zNear / depth;
        }

        public void SetupFrustum(float xmin, float xmax, float ymax, float zProj)
        {
            // Handle X
            float lenght = (float)Math.Sqrt(xmax * xmax + zProj * zProj);
            float oppleg = xmax / lenght;
            float adjleg = zProj / lenght;

            frustum[0].normal = Vector3.Multiply(Origin.axis[0], oppleg);
            frustum[0].normal = VectorMA(frustum[0].normal, adjleg, Origin.axis[1]);
            frustum[1].normal = Vector3.Multiply(Origin.axis[0], oppleg);
            frustum[1].normal = VectorMA(frustum[1].normal, -adjleg, Origin.axis[1]);

            // Handle Y
            lenght = (float)Math.Sqrt(ymax * ymax + zProj * zProj);
            oppleg = ymax / lenght;
            adjleg = zProj / lenght;

            frustum[2].normal = Vector3.Multiply(Origin.axis[0], oppleg);
            frustum[2].normal = VectorMA(frustum[2].normal, adjleg, Origin.axis[2]);
            frustum[3].normal = Vector3.Multiply(Origin.axis[0], oppleg);
            frustum[3].normal = VectorMA(frustum[3].normal, -adjleg, Origin.axis[2]);

            for (int i = 0; i < 4; i++)
            {
                frustum[i].type = (int)cplaneType.PLANE_NON_AXIAL;
                frustum[i].dist = Vector3.Dot(Renderer.Instance.Camera.position, frustum[i].normal);
                frustum[i].signbits = SignbitsForPlane(frustum[i]);
            }
        }

        public static Vector3 VectorMA(Vector3 v, float s, Vector3 b)
        {
            Vector3 o = Vector3.Zero;
            o[0] = v[0] + b[0] * s;
            o[1] = v[1] + b[1] * s;
            o[2] = v[2] + b[2] * s;
            return o;
        }

        public static Vector2 VectorMA(Vector2 v, float s, Vector2 b)
        {
            Vector2 o = Vector2.Zero;
            o[0] = v[0] + b[0] * s;
            o[1] = v[1] + b[1] * s;
            return o;
        }

        private byte SignbitsForPlane(cplane_t plane)
        {
            int bits = 0;
            // for fast box on planeside test
            for (int j = 0; j < 3; j++)
            {
                if (plane.normal[j] < 0)
                    bits |= 1 << j;
            }
            return (byte)bits;
        }

        public enum cplaneType : int
        {
            // 0-2 are axial planes
            PLANE_X = 0,
            PLANE_Y = 1,
            PLANE_Z = 2,
            // 3-5 are non-axial planes snapped to the nearest
            PLANE_NON_AXIAL = 3
        }
    }

    public class Orientation
    {
    	public Vector3		origin = Vector3.Zero;			// in world coordinates
        public Vector3[] axis = new Vector3[3];		// orientation in world
        public Vector3 viewOrigin = Vector3.Zero;		// viewParms->or.origin in local coordinates
        public Matrix modelMatrix = Matrix.Identity;
    } 
}
