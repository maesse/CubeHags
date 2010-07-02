using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.gui.Controls;
using CubeHags.client.common;
using SlimDX;

namespace CubeHags.client.gui
{
    class InfoUI : Window
    {
        private Label MouseFocus = null;
        private Label Info = null;
        private bool mouselock = false;
        Button button3 = null;
        int clickcount = 0;

        trace_t trace;
        long traceTime;
        
        public InfoUI()
        {
            this.AlwaysVisible = true;
            panel.ScrollbarStyle = Misc.ScrollbarStyle.BOTH;
            this.panel.Layout = new FlowLayout(false);
            this.Title = "Hags Windowing system";
            MouseFocus = new Label("MouseFocus: No", this);
            this.panel.AddControl(MouseFocus);
            Info = new Label("Info her", this);
            panel.AddControl(Info);
            //this.panel.AddControl(new Label("TEsttt", this));
            //this.panel.AddControl(new Label("TEstttasdddddd", this));
            //this.panel.AddControl(new Label("TEstttsss", this));
            //this.panel.AddControl();
            Button button = new Button("Load Sourcemap", this);
            button.Selected += new Button.ButtonSelectedEvent(LoadMapEvent);
            this.panel.AddControl(button);
            Button button2 = new Button("Unload Sourcemap", this);
            button2.Selected += new Button.ButtonSelectedEvent(UnLoadMapEvent);
            this.panel.AddControl(button2);
            button3 = new Button("Clicks: 0", this);
            button3.Selected += new Button.ButtonSelectedEvent(IncrementClickCount);
            
            this.panel.AddControl(button3);


            Button button4 = new Button("Take Screenshot", this);
            this.panel.AddControl(button4);

            TextBox textbox = new TextBox(this);
            
            this.panel.AddControl(textbox);
        }

        public void IncrementClickCount()
        {
            clickcount++;
            button3.label.Text = "Clicks: " + clickcount;
        }

        public void LoadMapEvent()
        {

            //string str = Renderer.Instance.gui.OpenFileDialog("BSP Files|*.bsp|All Files|*.*");
            //if (str != null)
            //{
            //    Renderer.Instance.LoadMap(str);
            //    //Renderer.Instance.LoadMap(@"client/data/map/albjergparken.bsp");
            //}
            
        }

        public void SetPos(Vector3 pos)
        {
            Info.Text = string.Format("[x:{0:0000.0} y:{1:0000.0} z:{2:0000.0}]", pos.X, pos.Y, pos.Z);
        }

        public void HitWall(trace_t trace)
        {
            if (traceTime == 0 || ((HighResolutionTimer.Ticks - traceTime) / HighResolutionTimer.Frequency) > 1f)
            {
                //this.trace = trace;
                //Info.Text = "Hit Wall - " + trace.plane;
                traceTime = HighResolutionTimer.Ticks;
            }
        }

        public void UnLoadMapEvent()
        {
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "cinematic cube.avi\n");
            //Renderer.Instance.UnloadMap();
        }

        public override void Update()
        {
            // Check for changed value
            if (WindowManager.Instance.MouseLock != mouselock)
            {
                mouselock = WindowManager.Instance.MouseLock;
                MouseFocus.Text = "MouseFocus: " + ((mouselock) ? "Yes" : "No");
            }
            if (Renderer.Instance.SourceMap != null)
            {
                Info.Text = "CurrLeaf: " + Renderer.Instance.SourceMap.CurrentLeaf.cluster;
            }
            //Info.Text = string.Format("ControlsWithMouseEnter - n:{0}\n{1}", Window.ControlsWithMouseEnter.Count(), Window.ControlsWithMouseEnter);

            // button event: Renderer.Instance.LoadMap(@"client/data/map/albjergparken.bsp");
        }
        
    }
}
