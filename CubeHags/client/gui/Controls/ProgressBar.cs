using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.gui
{
    class ProgressBar : Control
    {
        public int Value
        {
            get { return _Value; }
            set
            {
                if (value > MaxValue) _Value = MaxValue;
                else if (value < MinValue) _Value = MinValue;
                else _Value = value;
            }
        }
        private int _Value = 0;
        public int MaxValue = 100;
        public int MinValue = 0;
        
        private Sprite sprite;
        private Texture pixmap;
        private Texture progress;
        private Rectangle[] coords;
        private Color white = Color.White;
        private Color green = Color.Green;

        public ProgressBar(Window window) : base(window)
        {
            sprite = new Sprite(Renderer.Instance.device);
            pixmap = TextureManager.Instance.LoadTexture("window-theme/progressbar.png");
            progress = TextureManager.Instance.LoadTexture("window-theme/progress.png");
            coords = new Rectangle[9];
            coords[0] = new Rectangle(0 ,0 ,5,5);
            coords[1] = new Rectangle(5, 0, 5, 5);
            coords[2] = new Rectangle(10, 0, 5, 5);
            coords[3] = new Rectangle(0, 5, 5, 5);
            coords[4] = new Rectangle(5, 5, 5, 5);
            coords[5] = new Rectangle(10, 5, 5, 5);
            coords[6] = new Rectangle(0, 15, 5, 5);
            coords[7] = new Rectangle(5, 15, 5, 5);
            coords[8] = new Rectangle(10, 15, 5, 5);
            PreferredSize.Height = 20;
            PreferredSize.Width = MAXSIZE;
        }

        public override void Render()
        {
            white = Color.FromArgb((int)(Window.Opacity*255), white);

            sprite.Begin(SpriteFlags.AlphaBlend);
            // top
            //sprite.Draw2D(pixmap, coords[0], new SizeF(5,5), new PointF(Position.X, Position.Y), white);
            //sprite.Draw2D(pixmap, coords[1], new SizeF(Size.Width - 10, 5), new PointF(Position.X + 5, Position.Y), white);
            //sprite.Draw2D(pixmap, coords[2], new SizeF(5, 5), new PointF(Position.X + Size.Width - 5, Position.Y), white);

            ////// mid
            //sprite.Draw2D(pixmap, coords[3], new SizeF(5, Size.Height - 10), new PointF(Position.X, Position.Y + 5), white);
            //sprite.Draw2D(pixmap, coords[4], new SizeF(Size.Width - 10, Size.Height - 10), new PointF(Position.X + 5, Position.Y + 5), white);
            //sprite.Draw2D(pixmap, coords[5], new SizeF(5, Size.Height - 10), new PointF(Position.X + Size.Width - 5, Position.Y + 5), white);

            //// bot
            //sprite.Draw2D(pixmap, coords[6], new SizeF(5, 5), new PointF(Position.X, Position.Y + Size.Height - 5), white);
            //sprite.Draw2D(pixmap, coords[7], new SizeF(Size.Width - 10, 5), new PointF(Position.X + 5, Position.Y + Size.Height - 5), white);
            //sprite.Draw2D(pixmap, coords[8], new SizeF(5, 5), new PointF(Position.X + Size.Width - 5, Position.Y + Size.Height - 5), white);

            int tempMax = MaxValue - MinValue;
            int tempVal = Value - MinValue;
            float pos = (float)tempVal / (float)tempMax; // 0 -> 1 pos

            // Progress
            //if(pos > 0f)
            //    sprite.Draw2D(progress, new Rectangle(), new SizeF((Size.Width - 8) * pos, Size.Height - 8), new PointF(Position.X + 4, Position.Y + 4), white);
            sprite.End();
        }

        public override Size GetPreferredSize()
        {
            return PreferredSize;
        }

        public override Size GetMinimumSize()
        {
            return new Size(50, 20);
        }

        public override Size GetMaximumSize()
        {
            return MaxSize;
        }
    }
}
