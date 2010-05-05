using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.input;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.gui
{
    class Button : Container
    {
        public Label label;
        public delegate void ButtonSelectedEvent();
        public ButtonSelectedEvent Selected = null;
        private int buttonTexture = 0;
        private Texture[] buttonTextures;
        private Rectangle[] regions;

        public Button(string Text, Window window)
            : base(window)
        {
            Selected = new ButtonSelectedEvent(ButtonSelected);
            Margin = 4;
            FlowLayout layout = new FlowLayout(true);
            layout.centered = false;
            label = new Label(Text, window);
            label.Color = Color.LightGray;
            this.AddControl(label);
            this.Name = "Button";
            Color = System.Drawing.Color.White;
            buttonTextures = new Texture[3];
            //buttonTextures[0] = TextureManager.Instance.LoadTexture("window-theme/button/button-default.png");
            //buttonTextures[1] = TextureManager.Instance.LoadTexture("window-theme/button/button-prelight.png");
            //buttonTextures[2] = TextureManager.Instance.LoadTexture("window-theme/button/button-pressed.png");
            regions = new Rectangle[9];
            regions[0] = new Rectangle(0, 0, 5, 5);
            regions[1] = new Rectangle(5, 0, 32, 5);
            regions[2] = new Rectangle(37, 0, 5, 5);

            regions[3] = new Rectangle(0, 5, 5, 13);
            regions[4] = new Rectangle(5, 5, 32, 13);
            regions[5] = new Rectangle(37, 5, 5, 13);

            regions[6] = new Rectangle(0, 18, 5, 5);
            regions[7] = new Rectangle(5, 18, 32, 5);
            regions[8] = new Rectangle(37, 18, 5, 5);
        }

        public virtual void ButtonSelected()
        {

        }

        public override void MouseEnterEvent(MouseEvent evt)
        {
            buttonTexture = 1;
        }

        public override void MouseExitEvent(MouseEvent evt)
        {
            buttonTexture = 0;
        }

        public override void MouseDownEvent(MouseEvent evt)
        {
            buttonTexture = 2;
        }

        public override void MouseUpEvent(MouseEvent evt)
        {
            // Go back to hightlight if mouse is still covering button
            if (Bound.Contains(evt.Position.X, evt.Position.Y))
            {
                buttonTexture = 1;
                Selected();
            }
            else
                buttonTexture = 0;
        }

        public override void Render()
        {
            //sprite.Begin(SpriteFlags.AlphaBlend);
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[0], regions[0].Size, new PointF(Position.X, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[1], new SizeF(Size.Width - regions[0].Width - regions[2].Width, regions[1].Height), new PointF(Position.X + regions[0].Width, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[2], regions[2].Size, new PointF(Position.X + Size.Width - regions[3].Width, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //sprite.Draw2D(buttonTextures[buttonTexture], regions[3], new SizeF(regions[3].Width, Size.Height - regions[1].Height - regions[7].Height), new PointF(Position.X, Position.Y + regions[0].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[4], new SizeF(Size.Width - regions[3].Width - regions[5].Width, Size.Height - regions[1].Height - regions[7].Height), new PointF(Position.X + regions[3].Width, Position.Y + regions[1].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[5], new SizeF(regions[5].Width, Size.Height - regions[1].Height - regions[7].Height), new PointF(Position.X + Size.Width - regions[5].Width, Position.Y + regions[2].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //sprite.Draw2D(buttonTextures[buttonTexture], regions[6], regions[6].Size, new PointF(Position.X, Position.Y + Size.Height - regions[6].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[7], new SizeF(Size.Width - regions[6].Width - regions[8].Width, regions[7].Height), new PointF(Position.X + regions[6].Width, Position.Y + Size.Height - regions[7].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(buttonTextures[buttonTexture], regions[8], regions[8].Size, new PointF(Position.X + Size.Width - regions[8].Width, Position.Y + Size.Height - regions[8].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //sprite.End();
            base.Render();
        }

        public override Size GetPreferredSize()
        {
            Size dim = Layout.PreferredLayoutSize(this) + new Size(Margin * 2, Margin * 2);
            if (dim.Height < 21)
                dim.Height = 21;
            if (dim.Width < 10)
                dim.Width = 10;
            return dim;
        }

        public override Size GetMinimumSize()
        {
            Size dim = Layout.MinimumLayoutSize(this) + new Size(Margin * 2, Margin * 2);
            if (dim.Height < 21)
                dim.Height = 21;
            if (dim.Width < 10)
                dim.Width = 10;
            return dim;
        }

        public override Size GetMaximumSize()
        {
            return MaxSize;
        }
    }
}
