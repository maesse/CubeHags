using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.input;
using SlimDX.Direct3D9;
using System.Drawing;
using CubeHags.client.gfx;
using CubeHags.client.render;

namespace CubeHags.client.gui
{
    class Button : Container
    {
        public enum ButtonState
        {
            NORMAL,
            HOVER,
            DOWN
        }
        public Label label;
        public delegate void ButtonSelectedEvent();
        public ButtonSelectedEvent Selected = null;
        private ButtonState state;
        private HagsAtlas atlas;
        private Dictionary<ButtonState, Point> stateOffets = new Dictionary<ButtonState, Point>();

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

            atlas = new HagsAtlas("window-theme/button/button.png");
            atlas["topleft"] = new Rectangle(0, 0, 3, 3);
            atlas["left"] = new Rectangle(0, 3, 3, 15);
            atlas["bottomleft"] = new Rectangle(0, 18, 3, 3);
            atlas["top"] = new Rectangle(3, 0, 34, 3);
            atlas["middle"] = new Rectangle(3, 3, 34, 15);
            atlas["bottom"] = new Rectangle(3, 18, 34, 3);
            atlas["topright"] = new Rectangle(37,0,3,3);
            atlas["right"] = new Rectangle(37, 3, 3, 15);
            atlas["bottomright"] = new Rectangle(37, 18, 3, 3);
            stateOffets.Add(ButtonState.HOVER, new Point(0, 21));
            stateOffets.Add(ButtonState.DOWN, new Point(0, 43));
        }

        public virtual void ButtonSelected()
        {

        }

        public override void MouseEnterEvent(MouseEvent evt)
        {
            state = ButtonState.HOVER;
        }

        public override void MouseExitEvent(MouseEvent evt)
        {
            state = ButtonState.NORMAL;
        }

        public override void MouseDownEvent(MouseEvent evt)
        {
            state = ButtonState.DOWN;
        }

        public override void MouseUpEvent(MouseEvent evt)
        {
            // Go back to hightlight if mouse is still covering button
            if (Bound.Contains(evt.Position.X, evt.Position.Y))
            {
                state = ButtonState.HOVER;
                Selected();
            }
            else
                state = ButtonState.NORMAL;
        }

        Rectangle MoveRectangle(Rectangle rect, Point point)
        {
            Rectangle recta = new Rectangle(rect.X + point.X, rect.Y + point.Y, rect.Width, rect.Height);
            return recta;
        }

        public override void Render()
        {
            int vertStartOffset = WindowManager.Instance.VertexList.Count;

            Point texOffset = Point.Empty;
            if(stateOffets.ContainsKey(state))
                texOffset = stateOffets[state];
            
            // Top
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y), new Size(atlas["topleft"].Width, atlas["topleft"].Height)), MoveRectangle(atlas["topleft"], texOffset), atlas.Texture.Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["topleft"].Width, Position.Y), new Size(Size.Width - atlas["topleft"].Width - atlas["topright"].Width, atlas["top"].Height)), MoveRectangle(atlas["top"], texOffset), atlas.Texture.Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["topright"].Width, Position.Y), new Size(atlas["topright"].Width, atlas["topright"].Height)), MoveRectangle(atlas["topright"], texOffset), atlas.Texture.Size));

            // Bottom
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y + Size.Height - atlas["bottomleft"].Height), atlas["bottomleft"].Size), MoveRectangle(atlas["bottomleft"], texOffset), atlas.Texture.Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["bottomleft"].Width, Position.Y + Size.Height - atlas["bottom"].Height), new Size(Size.Width - atlas["bottomleft"].Width - atlas["bottomright"].Width, atlas["bottom"].Height)), MoveRectangle(atlas["bottom"], texOffset), atlas.Texture.Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["bottomright"].Width, Position.Y + Size.Height - atlas["bottomright"].Height), atlas["bottomright"].Size), MoveRectangle(atlas["bottomright"], texOffset), atlas.Texture.Size));

            // Middle
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y + atlas["topleft"].Height), new Size(atlas["left"].Width, Size.Height - atlas["topleft"].Height - atlas["bottomleft"].Height)), MoveRectangle(atlas["left"], texOffset), atlas.Texture.Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["left"].Width, Position.Y + atlas["top"].Height), new Size(Size.Width - atlas["left"].Width - atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height)), MoveRectangle(atlas["middle"], texOffset), atlas.Texture.Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["right"].Width, Position.Y + atlas["topright"].Height), new Size(atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottomright"].Height)), MoveRectangle(atlas["right"], texOffset), atlas.Texture.Size));

            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, atlas.Texture.MaterialID, 0, 0, WindowManager.Instance.vb.VertexBufferID);
            int nPrimitives = (WindowManager.Instance.VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                if (setMaterial)
                    device.SetTexture(0, atlas.Texture.Texture);

                // Draw UI elements
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);
            });

            WindowManager.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
           
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
