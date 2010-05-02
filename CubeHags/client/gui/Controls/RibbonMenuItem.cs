using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.gui
{
    class RibbonMenuItem : Label
    {
        // Drawing
        private Sprite sprite;
        private bool _Showing = false;
        public bool Showing { get { return _Showing; } set { _Showing = value; } }

        private RibbonMenu main;
        private Panel panel;
        public List<RibbonGroup> Items = new List<RibbonGroup>();

        // Selection
        private long MoveOverStartTime = 0;
        public float MouseOverSelectionTime = 0.5f; // mouse over 1 secs time -> select item
        bool mouseDown = false;

        SizeF leftS = new SizeF();
        SizeF rightS = new SizeF();
        SizeF middleS = new SizeF();
        PointF leftP = new PointF();
        PointF middleP = new PointF();
        PointF rightP = new PointF();

        public RibbonMenuItem(string name, RibbonMenu main) : base(name, main)
        {
            this.main = main;

            FlowLayout flow = new FlowLayout(true);
            flow.Margin = 10;

            // Set up panel area
            panel = new Panel(main);
            panel.Layout = flow;
            panel.Position = new Dimension(10, 23);
            panel.Size = new Size(Renderer.Instance.RenderSize.Width - 20, 77);

            //sprite = new Sprite(Renderer.Instance.device);
        }

        public void AddItem(RibbonGroup item)
        {
            Items.Add(item);
            panel.AddControl(item);
            //panel.DoLayout();
        }

        public override void MouseUpEvent(CubeHags.client.input.MouseEvent evt)
        {
            if (mouseDown)
            {
                if (!Showing)
                    main.ShowItem(this);
                else
                    main.ToggleShowRibbon();
                mouseDown = false;
            }
            MoveOverStartTime = 0;
            
            base.MouseUpEvent(evt);
        }

        public override void MouseDownEvent(CubeHags.client.input.MouseEvent evt)
        {
            mouseDown = true;
            base.MouseDownEvent(evt);
        }

        public override void MouseExitEvent(CubeHags.client.input.MouseEvent evt)
        {
            mouseDown = false;
            MoveOverStartTime = 0;
            base.MouseExitEvent(evt);
        }

        public override void MouseEnterEvent(CubeHags.client.input.MouseEvent evt)
        {
            MoveOverStartTime = HighResolutionTimer.Ticks;
            base.MouseEnterEvent(evt);
        }

        public override void Render()
        {
            
            // positions
            float posY = 0f;
            int extraWidth = 6;
            leftS.Width = main.buttonRect[0].Width;
            leftS.Height = main.buttonRect[0].Height;
            rightS.Width = main.buttonRect[2].Width;
            rightS.Height = main.buttonRect[2].Height;
            middleS.Width = Size.Width - (leftS.Width + rightS.Width) + extraWidth;
            middleS.Height = main.buttonRect[1].Height;
            leftP.X =Position.X- (extraWidth/2);
            leftP.Y = posY;
            middleP.X = Position.X + leftS.Width - (extraWidth / 2);
            middleP.Y = posY;
            rightP.X = Position.X + leftS.Width + middleS.Width - (extraWidth / 2);
            rightP.Y = posY;
            if (!Showing && MoveOverStartTime > 0)
            {
                // Check hover time
                long totalTime = HighResolutionTimer.Ticks - MoveOverStartTime;
                if (((float)totalTime / HighResolutionTimer.Frequency) > MouseOverSelectionTime)
                {
                    main.ShowItem(this);
                }
                else
                {
                    //sprite.Begin(SpriteFlags.AlphaBlend);
                    //// Still not Showing, just hovering with mouse
                    //sprite.Draw2D(main.buttonTex, main.buttonRect[0], leftS, leftP, System.Drawing.Color.FromArgb(128, System.Drawing.Color.White));
                    //sprite.Draw2D(main.buttonTex, main.buttonRect[1], middleS, middleP, System.Drawing.Color.FromArgb(128, System.Drawing.Color.White));
                    //sprite.Draw2D(main.buttonTex, main.buttonRect[2], rightS, rightP, System.Drawing.Color.FromArgb(128, System.Drawing.Color.White));
                    //sprite.End();
                }
            }
            
            if (Showing)
            {
                //sprite.Begin(SpriteFlags.AlphaBlend);
                //// Draw button without antialiasing to show selection
                //// Still not Showing, just hovering with mouse
                //sprite.Draw2D(main.buttonTex, main.buttonRect[0], leftS, leftP, System.Drawing.Color.White);
                //sprite.Draw2D(main.buttonTex, main.buttonRect[1], middleS, middleP, System.Drawing.Color.White);
                //sprite.Draw2D(main.buttonTex, main.buttonRect[2], rightS, rightP, System.Drawing.Color.White);
                //sprite.End();

                panel.Render();
            }

            
            base.Render();
        }
    }
}
