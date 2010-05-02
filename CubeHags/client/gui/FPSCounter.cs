using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CubeHags.client.gui
{
    class FPSCounter : Window
    {
        Label label;

        public FPSCounter()
        {
            panel.Layout = new FlowLayout(false);
            Title = "FPS Counter";
            label = new Label("FPS: N/A", this);
            panel.AddControl(label);
            Resizeable = false;
            WindowSpawnPosition = Corner.TOPRIGHT;
        }

        public override void Render()
        {
            label.Text = "FPS: " + HighResolutionTimer.Instance.FramesPerSecond;
            base.Render();
        }
    }
}
