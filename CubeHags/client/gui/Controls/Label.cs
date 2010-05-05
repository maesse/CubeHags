using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;
using CubeHags.client.input;
using CubeHags.client.render;

namespace CubeHags.client.gui
{
    public class Label : Control
    {
        public Align Alignment;
        private string oldText = "";
        private string _Text = "";
        public string Text { get { return _Text; } set { if (!_Text.Equals(value)) {
            oldText = _Text;
            _Text = value;
            if (oldText.Length != _Text.Length)
                Window.LayoutUpdate(true);
        } } }
        

        public enum Align
        {
            LEFT,
            CENTER,
            RIGHT
        }

        public Label(string text, Window window)
            : this(text, 10f, System.Drawing.Color.White, window)
        {
            
        }

        public Label(string text, float fontSize, System.Drawing.Color fontColor, Window window) : base(window)
        {
            this.Name = "Label";
            Text = text;
            Alignment = Align.LEFT;
            Color = fontColor;
        }

        public Label(string text, Align Alignment, Window window) : this(text, window)
        {
            this.Alignment = Alignment;
        }

        // Checks if size has changed
        public void ValidateSize()
        {

        }

        public override void Render()
        {
                ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, 0, 0, 0, WindowManager.Instance.vb.VertexBufferID);
                RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
                {
                    Sprite sprite = Renderer.Instance.sprite;
                    sprite.Begin(SpriteFlags.AlphaBlend | SpriteFlags.DoNotAddRefTexture);
                    Renderer.Instance.Fonts["label"].DrawString(sprite, Text, Bound, DrawTextFormat.Left, System.Drawing.Color.FromArgb((int)(Window.Opacity * 255), Color));
                    sprite.End();
                });
                WindowManager.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        public override Size GetPreferredSize()
        {
            int x=0, y=0;
            Rectangle rect = new Rectangle();
            Renderer.Instance.Fonts["label"].MeasureString(Renderer.Instance.sprite, Text, DrawTextFormat.Left, ref rect);
            x = rect.Width;
            y = rect.Height;

            if (PreferredSize.Width > x)
                x = PreferredSize.Width;

            if (PreferredSize.Height > y)
                y = PreferredSize.Height;

            return new Size(x, y);
        }

        public override Size GetMinimumSize()
        {
            Rectangle rect = new Rectangle();

            Renderer.Instance.Fonts["label"].MeasureString(Renderer.Instance.sprite, Text, DrawTextFormat.Left, ref rect);
            return new Size(rect.Width, rect.Height);
        }

        public override Size GetMaximumSize()
        {
            return MaxSize;
        }
    }
}
