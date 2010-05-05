using System;
using System.Collections.Generic;
 
using System.Text;
using System.Drawing;

namespace CubeHags.client.gui
{
    class ProfilerUI : Window
    {
        Dictionary<string, Label> labels = new Dictionary<string, Label>();
        Dictionary<string, ProgressBar> progressBars = new Dictionary<string, ProgressBar>();

        public ProfilerUI()
        {
            panel.Layout = new FlowLayout(false);
            this.Title = "CubeHags Profiler";
            this.PreferredSize = new Size(200, 100);
        }

        public override void Render()
        {
            Dictionary<string, long> timings = Profiler.Instance.GetTimings();
            foreach (string str in timings.Keys)
            {
                if (!labels.ContainsKey(str))
                {
                    Label label = new Label(str, this);
                    ProgressBar prog = new ProgressBar(this);
                    prog.MaxValue = 1000;
                    labels.Add(str, label);
                    progressBars.Add(str, prog);
                    panel.AddControl(label);
                    panel.AddControl(prog);
                }
                else
                {

                    progressBars[str].Value = (int)(timings[str] / (System.Diagnostics.Stopwatch.Frequency/(1000L*1000L)));
                }
                
            }
            base.Render();
        }
    }
}
