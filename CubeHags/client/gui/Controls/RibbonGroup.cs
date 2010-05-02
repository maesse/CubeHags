using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.gui
{
    class RibbonGroup : Container
    {
        RibbonMenu menu;
        Label label;

        public RibbonGroup(string Name, RibbonMenu menu) : base(menu)
        {
            label = new Label(Name, 8f, Color.Gray, menu);
            
            label.Size = label.GetPreferredSize();
            this.Name = Name;
            Layout = new FlowLayout(false);
            this.menu = menu;
            this.PreferredSize = new System.Drawing.Size(20, 66);
            this.MaxSize = new Size(Renderer.Instance.RenderSize.Width, 66);
        }

        public override void Render()
        {

            Position.X -= 3;
            Position.Y -= 3;
            //Size.Width += 6;
            //Size.Height += 6;
            //menu.sprite.Begin(SpriteFlags.AlphaBlend);

            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[0], menu.groupRect[0].Size, new PointF(Position.X, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[1], new SizeF(Size.Width - menu.groupRect[0].Width - menu.groupRect[2].Width, menu.groupRect[1].Height), new PointF(Position.X + menu.groupRect[0].Width, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[2], menu.groupRect[2].Size, new PointF(Position.X + Size.Width - menu.groupRect[3].Width, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[3], new SizeF(menu.groupRect[3].Width, Size.Height - menu.groupRect[1].Height - menu.groupRect[7].Height), new PointF(Position.X, Position.Y + menu.groupRect[0].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[4], new SizeF(Size.Width - menu.groupRect[3].Width - menu.groupRect[5].Width, Size.Height - menu.groupRect[1].Height - menu.groupRect[7].Height), new PointF(Position.X + menu.groupRect[3].Width, Position.Y + menu.groupRect[1].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[5], new SizeF(menu.groupRect[5].Width, Size.Height - menu.groupRect[1].Height - menu.groupRect[7].Height), new PointF(Position.X + Size.Width - menu.groupRect[5].Width, Position.Y + menu.groupRect[2].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[6], menu.groupRect[6].Size, new PointF(Position.X, Position.Y + Size.Height - menu.groupRect[6].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[7], new SizeF(Size.Width - menu.groupRect[6].Width - menu.groupRect[8].Width, menu.groupRect[7].Height), new PointF(Position.X + menu.groupRect[6].Width, Position.Y + Size.Height - menu.groupRect[7].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //menu.sprite.Draw2D(menu.groupTex, menu.groupRect[8], menu.groupRect[8].Size, new PointF(Position.X + Size.Width - menu.groupRect[8].Width, Position.Y + Size.Height - menu.groupRect[8].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            
            //menu.sprite.End();
            label.Position = new Dimension(Position.X + (Size.Width / 2) - (label.Size.Width / 2), Position.Y + Size.Height - label.Size.Height);
            label.Bound = new Rectangle(new Point(label.Position.X, label.Position.Y), label.Size);
            label.Render();

            //Position.X += 3;
            //Size.Width -= 6;
            //Position.Y += 3;
            //Size.Height -= 6;
            
            base.Render();
        }
    }
}
