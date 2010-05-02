using System;
using System.Collections.Generic;
using System.Linq;
//using System.Windows.Forms;
using System.Threading;
using CubeHags.server;
using CubeHags.client;
using SlimDX.Windows;
using SlimDX;
using SlimDX.Direct3D9;
using System.Windows;
using CubeHags.common;
using CubeHags.client.common;

namespace CubeHags
{
    static class Program
    {
        //public class WPFApp : Application
        //{
        //    protected override void OnStartup(StartupEventArgs e)
        //    {
        //        base.OnStartup(e);

        //        Window1 window = new Window1();
        //        window.Show();
        //    }
        //}
        /// <summary>
        /// Opens the client and server
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            
            // Set the initial time base
            Common.Instance.Milliseconds();
            Common.Instance.Init(Commands.ArgsFrom(args, 0));
            Net.Instance.Init();
            //float minMsec = 10f;
            //Renderer render = Renderer.Instance;
            //long lastFrameTime = HighResolutionTimer.Ticks;
            //float lastUpdate = 0f;
            //Device device = Renderer.Instance.device;
            Commands.Instance.AddText("map cs_office\n");
            MessagePump.Run(Renderer.Instance.form, () =>
            {
                Common.Instance.Frame();
                //HighResolutionTimer.Instance.Set();
                //float delta = (float)(HighResolutionTimer.Ticks - lastFrameTime)/HighResolutionTimer.Frequency;
                //lastFrameTime = HighResolutionTimer.Ticks;
                ////lastUpdate = HighResolutionTimer.Instance.GetDeltaTicks();
                //try
                //{
                //    Input.Instance.Update();
                //    render.Camera.Update(delta);
                //    render.Render();
                //}
                //catch (SlimDX.Direct3D9.Direct3D9Exception e)
                //{
                //    System.Console.WriteLine(e.ToString());
                //    if (e.ResultCode == SlimDX.Direct3D9.ResultCode.DeviceLost)
                //    {
                //        render.deviceLost = true;
                //        render.OnLostDevice();
                //    }
                //    else
                //        throw;
                //}
            });

            // Cleanup
            foreach (var item in ObjectTable.Objects)
                item.Dispose();
        }

        //static void openServer()
        //{
        //    using (ServerGUI form = new ServerGUI())
        //    {
        //        form.Show();
        //        Application.Run(form);
        //    }
        //}
    }
}
