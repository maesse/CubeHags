using System;
using SlimDX;
using SlimDX.Windows;
using CubeHags.common;
using CubeHags.client;
using CubeHags.client.common;

namespace CubeHags
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            
            // Set the initial time base
            Common.Instance.Milliseconds();
            Common.Instance.Init(Commands.ArgsFrom(args, 0));
            Net.Instance.Init();
            MessagePump.Run(Renderer.Instance.form, () =>
            {
                Common.Instance.Frame();
            });
            
            // Cleanup
            foreach (var item in ObjectTable.Objects)
                item.Dispose();
        }
    }
}
