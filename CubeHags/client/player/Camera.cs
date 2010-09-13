using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using SlimDX;
//using SlimDX.DirectInput;
using CubeHags.client.gui;
using CubeHags.client.input;
using System.Windows.Forms;
using CubeHags.client.render;
using CubeHags.client.player;

namespace CubeHags.client
{
    public class Camera
    {
        private SlimDX.Direct3D9.Device device;
        public Vector3 position = new Vector3(0f,0f,0f); // camera position
        
        //Matrix worldMatrix = new Matrix();
        public Matrix View = new Matrix();
        public Matrix World { get { return device.GetTransform(TransformState.World); } }
        public Matrix Projection { get { return device.GetTransform(TransformState.Projection); } }
        public Frustrum Frustrum = new Frustrum();

        // Constructor
        public Camera(SlimDX.Direct3D9.Device device)
        {
            this.device = device;
            //worldMatrix[0, 2] = -1f;
            //worldMatrix[1, 0] = -1f;
            //worldMatrix[2, 1] = 1f;
            //worldMatrix[3, 3] = 1f;
        }

        // Positions the camera
        public void RotateForViewer()
        {
            if (Renderer.Instance.viewParams == null)
                return;

            CubeHags.client.render.Orientation or = new render.Orientation();
            or.viewOrigin = Renderer.Instance.viewParams.Origin.origin;
            position = or.viewOrigin;

            Vector3[] axis = Renderer.Instance.viewParams.viewaxis;
            View.M11 = axis[0].X; View.M12 = axis[1].X; View.M13 = axis[2].X; View.M14 = 0f;
            View.M21 = axis[0].Y; View.M22 = axis[1].Y; View.M23 = axis[2].Y; View.M24 = 0f;
            View.M31 = axis[0].Z; View.M32 = axis[1].Z; View.M33 = axis[2].Z; View.M34 = 0f;
            View.M41 = -Vector3.Dot(or.viewOrigin, axis[0]);
            View.M42 = -Vector3.Dot(or.viewOrigin, axis[1]);
            View.M43 = -Vector3.Dot(or.viewOrigin, axis[2]);
            View.M44 = 1f;

            // convert from our coordinate system (looking down X)
            // to OpenGL's coordinate system (looking down -Z)
            View = Matrix.Multiply(View, flipMatrix);
            or.modelMatrix = View;
            Renderer.Instance.viewParams.World = or;

            //device.SetTransform(TransformState.World, worldMatrix);
            device.SetTransform(TransformState.View, or.modelMatrix);
            device.SetTransform(TransformState.Projection, Renderer.Instance.viewParams.ProjectionMatrix);
            //Frustrum.SetupFrustrum(this);
        }

       public static Matrix flipMatrix = new Matrix()
        {
            M11 =  0, M12 = 0, M13 = -1, M14 = 0,
            M21 = -1, M22 = 0, M23 =  0, M24 = 0,
            M31 =  0, M32 = 1, M33 =  0, M34 = 0,
            M41 =  0, M42 = 0, M43 =  0, M44 = 1
            // convert from our coordinate system (looking down X)
            // to OpenGL's coordinate system (looking down -Z)
        };
    }
}
