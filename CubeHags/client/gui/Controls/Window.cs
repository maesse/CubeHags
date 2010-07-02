using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX;
using SlimDX.Direct3D9;
using System.Drawing;
using SlimDX.DirectInput;
using CubeHags.client.input;
using CubeHags.client.gfx;
using CubeHags.client.map.Source;
using CubeHags.client.render;

namespace CubeHags.client.gui
{
    
    public class Window : Control
    {
        private string _Title = "Window";
        public string Title { get { return _Title; } set { _Title = value; UpdateTitleSize(); } }

        public bool Resizeable = true;
        public bool Borderless = false;
        public Corner WindowSpawnPosition = Corner.NONE;
        public bool AutoResize = true;
        public bool AcceptFocus = true;
        public bool AlwaysVisible = false;
        
        // Panel
        public Panel panel;
        public bool ForcePanelSize = false;
        public Size PanelSize = new Size();
        public int Zvalue = -1;
        public int BorderMargin = 4;

        // Text
        private Rectangle TitleSize = Rectangle.Empty;

        // Window graphics
        public float Opacity = 1f;
        HagsAtlas atlas;

        // Dragging
        private bool dragging = false;
        private Corner dragCorner;
        private Dimension dragPosition = null;
        private Size dragOriginalSize;

        // Focus
        public Control FocusControl 
        {
            get { return _FocusControl; }
            set
            {
                // Remove focus from old control
                if (value != _FocusControl && _FocusControl != null)
                {
                    _FocusControl.HasFocus = false;
                }
                // Add to new
                _FocusControl = value;
                _FocusControl.HasFocus = true;
            }
        }
        private Control _FocusControl = null;
        private Control MouseLockControl = null;
        public bool MouseLock { get { return _MouseLock; } }
        private bool _MouseLock = false;

        // Corner bounds
        Rectangle topleft = new Rectangle();
        Rectangle botright = new Rectangle();
        Rectangle topright = new Rectangle();
        Rectangle botleft = new Rectangle();

        Rectangle left = new Rectangle();
        Rectangle right = new Rectangle();
        Rectangle bot = new Rectangle();
        Rectangle top = new Rectangle();
        Rectangle title = new Rectangle();
        

        // Control focus stuff - same principle as the key event generation code in Input
        public List<Control> ControlsWithMouseEnter = new List<Control>();

        public Window() : base(null)
        {
            this.Window = this;
            panel = new Panel(this);

            // Load theme
            // "Untitled-1.png"
            atlas = new HagsAtlas("window-theme/window-borders.png");
            
            //HagsAtlas.SerializeToXML(atlas);
            atlas["topleft"] = new Rectangle(0, 0, 26, 26);
            atlas["top"] = new Rectangle(0, 81, 128, 26);
            atlas["topright"] = new Rectangle(27, 0, 27, 26);

            atlas["left"] = new Rectangle(0, 28, 27, 26);
            atlas["middle"] = new Rectangle(55, 28, 25, 25);
            atlas["right"] = new Rectangle(54, 1, 27, 26);

            atlas["bottomleft"] = new Rectangle(0, 54, 27, 27);
            atlas["bottom"] = new Rectangle(27, 54, 27, 27);
            atlas["bottomright"] = new Rectangle(54, 54, 27, 27);
            atlas["scale"] = new Rectangle(27, 27, 17, 17);

            LayoutUpdate(false);
        }

        public virtual void Update()
        {
            
        }

        public override void Render()
        {
            if (!Borderless)
            {
                int vertStartOffset = WindowManager.Instance.VertexList.Count;

                // Top
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y), new Size(atlas["topleft"].Width, atlas["topleft"].Height)), atlas["topleft"], atlas.Texture.Size));
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["topleft"].Width, Position.Y), new Size(Size.Width - atlas["topleft"].Width - atlas["topright"].Width, atlas["top"].Height)), atlas["top"], atlas.Texture.Size));
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["topright"].Width, Position.Y), new Size(atlas["topright"].Width, atlas["topright"].Height)), atlas["topright"], atlas.Texture.Size));

                // Bottom
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y + Size.Height - atlas["bottomleft"].Height), atlas["bottomleft"].Size), atlas["bottomleft"], atlas.Texture.Size));
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["bottomleft"].Width, Position.Y + Size.Height - atlas["bottom"].Height), new Size(Size.Width - atlas["bottomleft"].Width - atlas["bottomright"].Width, atlas["bottom"].Height)), atlas["bottom"], atlas.Texture.Size));
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["bottomright"].Width, Position.Y + Size.Height - atlas["bottomright"].Height), atlas["bottomright"].Size), atlas["bottomright"], atlas.Texture.Size));

                // Middle
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X, Position.Y + atlas["topleft"].Height), new Size(atlas["left"].Width, Size.Height - atlas["topleft"].Height - atlas["bottomleft"].Height)), atlas["left"], atlas.Texture.Size));
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + atlas["left"].Width, Position.Y + atlas["top"].Height), new Size(Size.Width - atlas["left"].Width - atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height)), atlas["middle"], atlas.Texture.Size));
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["right"].Width, Position.Y + atlas["topright"].Height), new Size(atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottomright"].Height)), atlas["right"], atlas.Texture.Size));

                // Resizeable
                if (Resizeable)
                    WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new Rectangle(new Point(Position.X + Size.Width - atlas["scale"].Width - 2, Position.Y + Size.Height - atlas["scale"].Height - 2), atlas["scale"].Size), atlas["scale"], atlas.Texture.Size));

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
                    // Draw Title
                    Sprite sprite = Renderer.Instance.sprite;
                    sprite.Begin(SpriteFlags.AlphaBlend | SpriteFlags.DoNotAddRefTexture);
                    int yOffset = -(TitleSize.Height - atlas["top"].Height) / 2;
                    Renderer.Instance.Fonts["title"].DrawString(sprite, Title, new Rectangle(Position.X + atlas["topleft"].Width - 9, Position.Y + yOffset + 1, Size.Width - (2 * 32), TitleSize.Height), DrawTextFormat.SingleLine | DrawTextFormat.NoClip | DrawTextFormat.Center, Color.FromArgb((int)(Opacity * 255), Color.FromArgb(50, 50, 50)));
                    Renderer.Instance.Fonts["title"].DrawString(sprite, Title, new Rectangle(Position.X + atlas["topleft"].Width - 10, Position.Y + yOffset, Size.Width - (2 * 32), TitleSize.Height), DrawTextFormat.SingleLine | DrawTextFormat.NoClip | DrawTextFormat.Center, Color);
                    sprite.End();
                    //device.ScissorRect = oldrect;
                });

                WindowManager.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
            }

            // Render window content
            panel.Render();
        }

        private void HandleWindowDragEvent(MouseEvent evt, int relX, int relY)
        {
            if (dragCorner == Corner.TITLE)
            {
                // Drag movement
                Move((int)evt.dX, (int)evt.dY);
                WindowManager.Instance.Cursor = WindowManager.CursorType.MOVE;
            }
            else if (dragCorner == Corner.BOTRIGHT)
            {
                WindowManager.Instance.Cursor = WindowManager.CursorType.SCALE;
                // only make size bigger if mouse is near the borders
                float dx = ((evt.dX > 0) ? ((Size.Width - relX < dragPosition.X) ? evt.dX : 0) : evt.dX);
                float dy = ((evt.dY > 0) ? ((Size.Height - relY < dragPosition.Y) ? evt.dY : 0) : evt.dY);
                System.Console.WriteLine(evt.dX + ":" + evt.dY);
                ResizeDelta(new Size((int)dx, (int)dy));
            }
            else if (dragCorner == Corner.RIGHT)
            {
                WindowManager.Instance.Cursor = WindowManager.CursorType.VERT;
                // only make size bigger if mouse is near the borders
                float dx = ((evt.dX > 0) ? ((Size.Width - relX < dragPosition.X) ? evt.dX : 0) : evt.dX);
                ResizeDelta(new Size((int)dx, 0));
            }
            else if (dragCorner == Corner.BOT)
            {
                WindowManager.Instance.Cursor = WindowManager.CursorType.HORIZ;
                // only make size bigger if mouse is near the borders
                float dy = ((evt.dY > 0) ? ((Size.Height - relY < dragPosition.Y) ? evt.dY : 0) : evt.dY);
                
                ResizeDelta(new Size(0, (int)dy));
            }
            else if (dragCorner == Corner.LEFT)
            {
                WindowManager.Instance.Cursor = WindowManager.CursorType.VERT;
                // only make size bigger if mouse is near the borders
                float dx = ((evt.dX < 0) ? ((relX < -(dragPosition.X - dragOriginalSize.Width)) ? evt.dX : 0) : evt.dX);
                Size tempSize = Size;
                if (ResizeDelta(new Size((int)-dx, 0)))
                    Move(tempSize.Width - Size.Width, 0);
            }
            else if (dragCorner == Corner.TOP)
            {
                WindowManager.Instance.Cursor = WindowManager.CursorType.HORIZ;
                // only make size bigger if mouse is near the borders
                float dy = ((evt.dY < 0) ? ((relY < -(dragPosition.Y - dragOriginalSize.Height)) ? evt.dY : 0) : evt.dY);
                Size tempSize = Size;
                if (ResizeDelta(new Size(0, (int)-dy)))
                    Move(0, tempSize.Height - Size.Height);
            }
        }

        // Handle mouse movement event
        public override void MouseMoveEvent(MouseEvent evt)
        {
            // Special case for mouselock
            if (MouseLock)
            {
                MouseLockControl.MouseMove(evt);
            }
            else
            {
                // Set cursor if not dragging, and not borderless
                bool setCursor = (dragging || Borderless ? false : true);
                Corner corner = CheckIsMouseHitBorders(evt.Position, setCursor);

                int relX = evt.Position.X - Position.X;
                int relY = evt.Position.Y - Position.Y;
                // Handle dragging
                if (dragging && dragCorner != Corner.MIDDLE && !Borderless)
                {
                    HandleWindowDragEvent(evt, relX, relY);
                }
                // Else send event to panel
                else if (corner == Corner.MIDDLE || Borderless)
                {
                    // Send events to panel
                    panel.MouseMove(evt);
                }

                // Update list over controls with mouseenter
                List<Control> toremove = new List<Control>();
                foreach (Control control in ControlsWithMouseEnter)
                {
                    // Is mouse not within control anymore?
                    if (!control.Bound.Contains(evt.Position.X, evt.Position.Y))
                        toremove.Add(control);
                }
                // Send mouseexit events to controls that lost mouseenter-state
                foreach (Control control in toremove)
                {
                    control.MouseExit(evt);
                    ControlsWithMouseEnter.Remove(control);
                }
            }
            
        }

        // Window lost mouse coverage and mush flush its cache of controls with mouseenter
        public override void MouseExitEvent(MouseEvent evt)
        {

            if (!MouseLock)
            {
                foreach (Control control in ControlsWithMouseEnter)
                {
                    control.MouseExit(evt);
                }
                ControlsWithMouseEnter.Clear();
                WindowManager.Instance.Cursor = WindowManager.CursorType.NORMAL;
            }
            else
            {
                MouseLockControl.MouseExit(evt);
            }
        }

        public override void KeyDownEvent(KeyEvent evt)
        {
            if (FocusControl != null)
                FocusControl.KeyDown(evt);
        }

        // Handle mouse downbutton event
        public override void MouseDownEvent(MouseEvent evt)
        {
            // Special case if a control has mouselock
            if (MouseLock)
            {
                MouseLockControl.MouseDown(evt);
            }
            else
            {
                // Check if a border were hit
                int relX = evt.Position.X - Position.X;
                int relY = evt.Position.Y - Position.Y;
                Corner corner = CheckIsMouseHitBorders(evt.Position, false);
                // Check if border is hit
                if (corner != Corner.MIDDLE && !Borderless)
                {
                    // Recieved buttonpress on a border
                    if (!dragging && evt.ButtonState[0] && (evt.Type & MouseEvent.EventType.BUTTON0CHANGED) > 0)
                    {
                        // Initialize dragging
                        dragCorner = corner;
                        dragging = true;
                        WindowManager.Instance.GetMouseLock(this);
                        dragPosition = new Dimension(Size.Width - relX, Size.Height - relY);
                        dragOriginalSize = Size;
                    }
                }
                else
                {
                    // Send mouse event to panel
                    panel.MouseDown(evt);
                }
            }
            
        }

        // Handle mouse buttonrelease event
        public override void MouseUpEvent(MouseEvent evt)
        {
            // Special case for mouselock
            if (MouseLock)
            {
                MouseLockControl.MouseUp(evt);
            }
            else
            {
                // Finalize dragging
                if (dragging && !evt.ButtonState[0])
                {
                    // Stop dragging
                    dragging = false;
                    WindowManager.Instance.ReleaseMouseLock();
                }

                // Send MouseUp event to controls if no border were hit
                Corner corner = CheckIsMouseHitBorders(evt.Position, false);
                if (corner == Corner.MIDDLE || Borderless)
                    panel.MouseUp(evt);
            }
        }

        public void LayoutUpdate(bool ControlChanged)
        {
            // Measure title
            if (TitleSize == null)
                UpdateTitleSize();

            Size WindowSize = GetPreferredSize();

            // Resize to fit controls
            if (AutoResize)
            {
                //if (ControlChanged)
                //{
                //    WindowSize = GetPreferredSize();
                //}
                
                if (WindowSize.Width > Size.Width)
                    this.Size.Width = WindowSize.Width;
                if (WindowSize.Height > Size.Height)
                    this.Size.Height = WindowSize.Height;
            }

            // Define border size
            Size borderSize = GetBorderSize();

            // Size left over for panel
            Size newSize = new Size(Size.Width - borderSize.Width, Size.Height - borderSize.Height);

            // Panel position
            int borderleft = (Borderless ? 0 : borderSize.Width / 2);
            int bordertop = (Borderless ? 0 : atlas["top"].Height);
            Dimension newPosition = new Dimension(Position.X + borderleft, Position.Y + bordertop);

            // Compare to last stuff.. does panel still fit? has position changed?
            if (ControlChanged || (!panel.Size.Equals(newSize) || !panel.Position.Equals(newPosition)))
            {
                if (!ForcePanelSize)
                    panel.Size = newSize;
                else
                    panel.Size = PanelSize;
                panel.Position = newPosition;
                this.Bound = new Rectangle(Position.X, Position.Y, this.Size.Width, this.Size.Height);
                panel.DoLayout();
            }
            UpdateBounds();
        }

        // Update border rectangles
        private void UpdateBounds()
        {
            topleft = new Rectangle(0, 0, BorderMargin, BorderMargin);
            botright = new Rectangle(Size.Width - 20, Size.Height - 20, 20, 20);
            topright = new Rectangle(Size.Width - BorderMargin, 0, BorderMargin, BorderMargin);
            botleft = new Rectangle(0, Size.Height - BorderMargin, BorderMargin, BorderMargin);

            left = new Rectangle(0, BorderMargin, BorderMargin, Size.Height - (BorderMargin * 2));
            right = new Rectangle(Size.Width - BorderMargin, BorderMargin, BorderMargin, Size.Height - 20 - BorderMargin);
            bot = new Rectangle(BorderMargin, Size.Height - BorderMargin, Size.Width - 20 - BorderMargin, BorderMargin);
            top = new Rectangle(BorderMargin, 0, Size.Width - (BorderMargin * 2), BorderMargin);
            title = new Rectangle(BorderMargin, BorderMargin, Size.Width - 22 - (2 * BorderMargin), 20 + BorderMargin);
        }

        // Moves window on the screen
        public void Move(int dx, int dy)
        {
            Move(new Dimension(Position.X + dx, Position.Y + dy));
        }

        public void Move(Dimension position)
        {
            Position = position;
            LayoutUpdate(false);
        }

        public bool ResizeDelta(Size newSize)
        {
            Size tempsize = Size;
            Size += newSize;
            LayoutUpdate(false);
            if (Size.Width == tempsize.Width && Size.Height == tempsize.Height)
                return false;
            return true;
        }

        public bool Resize(Size newSize)
        {
            Size tempsize = Size;
            Size = newSize;
            LayoutUpdate(false);
            this.Bound = new Rectangle(Position.X, Position.Y, this.Size.Width, this.Size.Height);
            if (Size.Width == tempsize.Width && Size.Height == tempsize.Height)
                return false;
            return true;
        }

        private void UpdateTitleSize()
        {
            try
            {
                Renderer.Instance.Fonts["title"].MeasureString(Renderer.Instance.sprite, Title, DrawTextFormat.NoClip | DrawTextFormat.SingleLine, ref TitleSize);
            }
            catch
            {
                System.Console.WriteLine("Could not find font \"title\"");
            }
        }

        private Corner CheckIsMouseHitBorders(Point pos, bool SetCursor)
        {
            int relX = pos.X - Position.X;
            int relY = pos.Y - Position.Y;
            return CheckIsMouseHitBorders(relX, relY, SetCursor);
        }

        private Corner CheckIsMouseHitBorders(int relX, int relY, bool setCursor)
        {

            if (title.Contains(relX, relY))
            {
                if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.MOVE;
                return Corner.TITLE;
            }
            else if (Resizeable)
            {
                if (botright.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.SCALE;
                    return Corner.BOTRIGHT;
                }
                else if (bot.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.HORIZ;
                    return Corner.BOT;
                }
                else if (botleft.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.SCALE;
                    return Corner.BOTLEFT;
                }
                else if (left.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.VERT;
                    return Corner.LEFT;
                }
                else if (right.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.VERT;
                    return Corner.RIGHT;
                }
                else if (topleft.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.SCALE;
                    return Corner.TOPLEFT;
                }
                else if (top.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.HORIZ;
                    return Corner.TOP;
                }
                else if (topright.Contains(relX, relY))
                {
                    if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.SCALE;
                    return Corner.TOPRIGHT;
                }
            }
            {
                if (setCursor) WindowManager.Instance.Cursor = WindowManager.CursorType.NORMAL;
                return Corner.MIDDLE;
            }
        }

        // Window size without any content
        private Size GetBorderSize()
        {
            if (Borderless)
                return new Size();
            else
                return new Size(4 * 2, 5 + atlas["top"].Height);
        }

        public override Size GetPreferredSize()
        {
            Size dim = GetBorderSize();
            Size panelDim = panel.GetPreferredSize();
            if (!Borderless && panelDim.Width < TitleSize.Width + 32)
                panelDim.Width = TitleSize.Width + 32;
            return dim+panelDim;
        }

        public override Size GetMinimumSize()
        {
            Size dim = GetBorderSize();
            Size panelDim = panel.GetMinimumSize();
            if (!Borderless && panelDim.Width < TitleSize.Width + 28)
                panelDim.Width = TitleSize.Width + 28;
            return dim + panelDim;
        }

        public override Size GetMaximumSize()
        {
            return MaxSize;
        }

        // Sends all mouse event to a particular window until it releases its lock
        public bool GetMouseLock(Control control)
        {
            // Is the lock free?
            if (!_MouseLock && MouseLockControl == null)
            {
                WindowManager.Instance.GetMouseLock(this);
                MouseLockControl = control;
                _MouseLock = true;
                return true;
            }
            else
                return false;
        }

        public void ReleaseMouseLock()
        {
            WindowManager.Instance.ReleaseMouseLock();
            _MouseLock = false;
            MouseLockControl = null;
        }

        public Result OnLostDevice()
        {
            return atlas.OnLostDevice();
        }

        public Result OnResetDevice()
        {
            return atlas.OnResetDevice();
        }
    }
    public enum Corner
    {
        TOPLEFT,
        TOP,
        TOPRIGHT,
        LEFT,
        MIDDLE,
        RIGHT,
        BOTLEFT,
        BOT,
        BOTRIGHT,
        TITLE,
        NONE
    }
}
