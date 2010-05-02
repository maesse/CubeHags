using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CubeHags.client.gfx;
using CubeHags.client.gui.Misc;
using System.Drawing;
using CubeHags.client.render;
using SlimDX.Direct3D9;

namespace CubeHags.client.gui.Controls
{
    public class Scrollbar
    {
        public ScrollbarStyle ScrollbarStyle = ScrollbarStyle.NONE;
        private Size ScrollbarSize = new Size(64, 64); // Minimum size for panel the contains the scrollbars

        public Size ViewSize = new Size(); // Size we have been given by the parent
        public Point ViewPoint = new Point(); // View offset
        public Size ContentSize = new Size(); // Size of the contents

        private Container Parent;
        
        HagsAtlas atlas;

        public Scrollbar(Container parent)
        {
            this.Parent = parent;

            // Load texture and atlas
            atlas = new HagsAtlas("window-theme/scrollbar.png");
            atlas["toparrow"] = new System.Drawing.Rectangle(10, 0, 16, 16);
            atlas["bottomarrow"] = new System.Drawing.Rectangle(26, 0, 16, 16);
            atlas["background"] = new System.Drawing.Rectangle(16, 32, 1, 1);
            atlas["vslidermarks"] = new System.Drawing.Rectangle(0, 0, 9, 9);
            atlas["vslidertl"] = new System.Drawing.Rectangle(0, 16, 2, 2);
            atlas["vslidert"] = new System.Drawing.Rectangle(2, 16, 12, 2);
            atlas["vslidertr"] = new System.Drawing.Rectangle(14, 16, 2, 2);
            atlas["vsliderml"] = new System.Drawing.Rectangle(0, 18, 2, 28);
            atlas["vsliderm"] = new System.Drawing.Rectangle(2, 18, 12, 28);
            atlas["vslidermr"] = new System.Drawing.Rectangle(14, 18, 2, 28);
            atlas["vsliderbl"] = new System.Drawing.Rectangle(0, 46, 2, 2);
            atlas["vsliderb"] = new System.Drawing.Rectangle(2, 46, 12, 2);
            atlas["vsliderbr"] = new System.Drawing.Rectangle(14, 46, 2, 2);
            atlas["hslidermarks"] = new System.Drawing.Rectangle(17, 32, 9, 9);
            atlas["hslidertl"] = new System.Drawing.Rectangle(16, 16, 2, 2);
            atlas["hsliderl"] = new System.Drawing.Rectangle(16, 18, 2, 12);
            atlas["hsliderbl"] = new System.Drawing.Rectangle(16, 30, 2, 2);
            atlas["hslidert"] = new System.Drawing.Rectangle(18, 16, 28, 2);
            atlas["hsliderm"] = new System.Drawing.Rectangle(18, 18, 28, 12);
            atlas["hsliderb"] = new System.Drawing.Rectangle(18, 30, 28, 2);
            atlas["hslidertr"] = new System.Drawing.Rectangle(46, 16, 2, 2);
            atlas["hsliderr"] = new System.Drawing.Rectangle(46, 18, 2, 12);
            atlas["hsliderbr"] = new System.Drawing.Rectangle(46, 30, 2, 2);
        }

        public void Render()
        {
            if (ScrollbarStyle == Misc.ScrollbarStyle.NONE)
                return;

            int vertStartOffset = WindowManager.Instance.VertexList.Count;
            Dimension pos = Parent.Position;
            ViewSize = Parent.Size;
            float VisibleFrac = ViewSize.Height / ContentSize.Height;
            int slidermax = ViewSize.Height - atlas["toparrow"].Height - (atlas["bottomarrow"].Height * 2);
            Size sliderSize = new Size(atlas["vslidertl"].Width + atlas["vslidert"].Width + atlas["vslidertr"].Width, (int)(slidermax * VisibleFrac));
            Point sliderpos = new Point(pos.X + ViewSize.Width - atlas["bottomarrow"].Width, pos.Y + atlas["toparrow"].Height + (int)(((float)ViewPoint.Y / ViewSize.Height)*slidermax));
            if (ScrollbarStyle == ScrollbarStyle.BOTH)
            {
                // Background
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(pos.X + ViewSize.Width - atlas["toparrow"].Width, pos.Y), new Size(atlas["toparrow"].Width, ViewSize.Height - atlas["bottomarrow"].Height)), atlas["background"], atlas.Texture.Size));
                // Top arrow
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(pos.X + ViewSize.Width - atlas["toparrow"].Width, pos.Y), atlas["toparrow"].Size), atlas["toparrow"], atlas.Texture.Size));
                // Bottom arrow
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(pos.X + ViewSize.Width - atlas["bottomarrow"].Width, pos.Y + ViewSize.Height - (atlas["bottomarrow"].Height*2)), atlas["bottomarrow"].Size), atlas["bottomarrow"], atlas.Texture.Size));
                // VSlider
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(sliderpos.X + atlas["vsliderml"].Width, sliderpos.Y + atlas["vslidert"].Height), new Size(sliderSize.Width - atlas["vsliderml"].Width - atlas["vslidermr"].Width, sliderSize.Height - atlas["vslidert"].Height - atlas["vsliderb"].Height)), atlas["vsliderm"], atlas.Texture.Size));
            }
            // Top
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y), new Size(atlas["topleft"].Width, atlas["topleft"].Height)), atlas["topleft"], atlas.Texture.Size));
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["topleft"].Width, Position.Y), new Size(Size.Width - atlas["topleft"].Width - atlas["topright"].Width, atlas["top"].Height)), atlas["top"], atlas.Texture.Size));
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["topright"].Width, Position.Y), new Size(atlas["topright"].Width, atlas["topright"].Height)), atlas["topright"], atlas.Texture.Size));

            //// Bottom
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y + Size.Height - atlas["bottomleft"].Height), atlas["bottomleft"].Size), atlas["bottomleft"], atlas.Texture.Size));
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["bottomleft"].Width, Position.Y + Size.Height - atlas["bottom"].Height), new Size(Size.Width - atlas["bottomleft"].Width - atlas["bottomright"].Width, atlas["bottom"].Height)), atlas["bottom"], atlas.Texture.Size));
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["bottomright"].Width, Position.Y + Size.Height - atlas["bottomright"].Height), atlas["bottomright"].Size), atlas["bottomright"], atlas.Texture.Size));

            //// Middle
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y + atlas["topleft"].Height), new Size(atlas["left"].Width, Size.Height - atlas["topleft"].Height - atlas["bottomleft"].Height)), atlas["left"], atlas.Texture.Size));
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["left"].Width, Position.Y + atlas["top"].Height), new Size(Size.Width - atlas["left"].Width - atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height)), atlas["middle"], atlas.Texture.Size));
            //WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["right"].Width, Position.Y + atlas["topright"].Height), new Size(atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottomright"].Height)), atlas["right"], atlas.Texture.Size));

            //// Resizeable
            //if (Resizeable)
            //    WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["scale"].Width - 2, Position.Y + Size.Height - atlas["scale"].Height - 2), atlas["scale"].Size), atlas["scale"], atlas.Texture.Size));

            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, atlas.Texture.MaterialID, 0, 0, WindowManager.Instance.vb.VertexBufferID);
            int nPrimitives = (WindowManager.Instance.VertexList.Count - vertStartOffset) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                //Rectangle oldrect = device.ScissorRect;
                //device.ScissorRect = Bound;
                if (setMaterial)
                    device.SetTexture(0, atlas.Texture.Texture);

                // Draw UI elements
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);

                //device.ScissorRect = oldrect;
            });

            WindowManager.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        public Size GetScrollbarSize()
        {
            return ScrollbarSize;
        }
    }
}
