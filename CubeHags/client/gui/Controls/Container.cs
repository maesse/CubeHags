using System;
using System.Collections.Generic;
 
using System.Text;
using System.Drawing;
using CubeHags.client.input;
using CubeHags.client.gui.Misc;
using CubeHags.client.gui.Controls;

namespace CubeHags.client.gui
{
    /// Base class for widgets which contain other widgets
    public abstract class Container : Control
    {
        public int BorderWidth = 0;
        public LayoutManager Layout = new FlowLayout(true);
        
        private List<Control> _Controls = new List<Control>();
        public List<Control> Controls { get { return _Controls; } }
        public Control GetControl(int index)
        {
            return _Controls[index];
        }
        public int ControlCount { get { return _Controls.Count; } }

        public new Dimension Position { get { return base.Position; } set { base.Position = value; } }
        //public new Size Size { get { if (ScrollbarStyle == Misc.ScrollbarStyle.NONE) return base.Size; return Scrollbar.ContentSize; } set { if (ScrollbarStyle == Misc.ScrollbarStyle.NONE) base.Size = value; else Scrollbar.ContentSize = value; } }

        // Scrollbar implementation
        public ScrollbarStyle ScrollbarStyle { get { return _ScrollbarStyle; } set { if (_ScrollbarStyle != value) SetScrollbar(value); } }
        private ScrollbarStyle _ScrollbarStyle = ScrollbarStyle.NONE;
        private Scrollbar Scrollbar = null;

        public bool AutoResize = true;
        
        
        public Container(Window window) : base(window)
        {
            MouseDown += new ControlEvents.MouseDown(ContainerMouseDownEvent);
            MouseMove += new ControlEvents.MouseMove(ContainerMouseMoveEvent);
            MouseUp += new ControlEvents.MouseUp(ContainerMouseUpEvent);
            MouseEnter += new ControlEvents.MouseEnter(ContainerMouseEnterEvent);
            MouseExit += new ControlEvents.MouseExit(ContainerMouseExitEvent);
            KeyDown += new ControlEvents.KeyDown(ContainerKeyDownEvent);
        }

        void SetScrollbar(ScrollbarStyle style)
        {
            // Save style
            _ScrollbarStyle = style;
            if (style == Misc.ScrollbarStyle.NONE)
            {
                if (Scrollbar != null)
                {
                    // Remove scrollbar
                    Scrollbar = null;
                    // Let window react on the change
                    Window.LayoutUpdate(true);
                }
            }
            else
            {
                // Create scrollbar
                if (Scrollbar == null)
                    Scrollbar = new Scrollbar(this);

                // Set style
                Scrollbar.ScrollbarStyle = style;
            }
        }

        // Metods to propagate events to all contained controls
        public void ContainerMouseDownEvent(MouseEvent evt)
        {
            foreach (Control control in Controls)
            {
                if (control.Bound.Contains(evt.Position.X, evt.Position.Y))
                {
                    control.MouseDown(evt);
                    Window.FocusControl = control;
                }
            }
        }
        public void ContainerMouseUpEvent(MouseEvent evt) {
            foreach (Control control in Controls)
            {
                if (control.Bound.Contains(evt.Position.X, evt.Position.Y))
                control.MouseUp(evt);
            }
        }
        public void ContainerMouseMoveEvent(MouseEvent evt) {
            foreach (Control control in Controls)
            {
                if (control.Bound.Contains(evt.Position.X, evt.Position.Y))
                {
                    if (!Window.ControlsWithMouseEnter.Contains(control))
                    {
                        Window.ControlsWithMouseEnter.Add(control);
                        control.MouseEnter(evt);
                    }
                    control.MouseMove(evt);
                }
            }
        }
        public void ContainerMouseEnterEvent(MouseEvent evt) {
            foreach (Control control in Controls)
            {
                if (control.Bound.Contains(evt.Position.X, evt.Position.Y))
                control.MouseEnter(evt);
            }
        }
        public void ContainerMouseExitEvent(MouseEvent evt) {
            foreach (Control control in Controls)
            {
                if (control.Bound.Contains(evt.Position.X, evt.Position.Y))
                control.MouseExit(evt);
            }
        }
        public void ContainerKeyDownEvent(KeyEvent evt)
        {
            foreach (Control control in Controls)
            {
                //if (control.Bound.Contains(evt.Position.X, evt.Position.Y))
                control.KeyDown(evt);
            }
        }
        

        public void AddControl(Control control)
        {
            _Controls.Add(control);
            control.parent = this;
            Window.LayoutUpdate(true);
            DoLayout();
        }

        public void RemoveControl(int index)
        {
            _Controls.RemoveAt(index);
            DoLayout();
        }

        public void RemoveControl(Control control)
        {
            _Controls.Remove(control);
            DoLayout();
        }
        
        virtual public void DoLayout()
        {
            Layout.LayoutContainer(this);
        }
        public Control GetControlAt(int x, int y)
        {
            throw new NotImplementedException();
        }
        public override Size GetMinimumSize()
        {
            //return Layout.MinimumLayoutSize(this);
            if (ScrollbarStyle == Misc.ScrollbarStyle.NONE)
                return Layout.MinimumLayoutSize(this);
            else
            {
                // Override the containers layout size
                if (Scrollbar == null)
                    Scrollbar = new Scrollbar(this);
                Size scrollsize = Scrollbar.GetScrollbarSize();
                Size layoutsize = Layout.MinimumLayoutSize(this);
                Scrollbar.ContentSize = layoutsize;

                if (ScrollbarStyle == Misc.ScrollbarStyle.HORIZONTAL)
                {
                    // Keep height
                    return new Size(scrollsize.Width, layoutsize.Height);
                }
                else if (ScrollbarStyle == Misc.ScrollbarStyle.VERTICAL)
                {
                    // Keep width
                    return new Size(layoutsize.Width, scrollsize.Height);
                }
                else
                {
                    return scrollsize;
                }

            }
        }

        public override Size GetPreferredSize()
        {
            if (ScrollbarStyle == Misc.ScrollbarStyle.NONE)
                return Layout.PreferredLayoutSize(this);
            else 
            {
                // Override the containers layout size
                if (Scrollbar == null)
                    Scrollbar = new Scrollbar(this);
                Size scrollsize = Scrollbar.GetScrollbarSize();
                Size layoutsize = Layout.PreferredLayoutSize(this);
                Scrollbar.ContentSize = layoutsize;

                if (ScrollbarStyle == Misc.ScrollbarStyle.HORIZONTAL)
                {
                    // Keep height
                    return new Size(scrollsize.Width, layoutsize.Height);
                }
                else if (ScrollbarStyle == Misc.ScrollbarStyle.VERTICAL)
                {
                    // Keep width
                    return new Size(layoutsize.Width, scrollsize.Height);
                }
                else
                {
                    return scrollsize;
                }

            }
            
        }

        public override SlimDX.Result OnLostDevice()
        {
            foreach (Control control in Controls)
            {
                control.OnLostDevice();
            }
            return SlimDX.Result.Last;
        }

        public override SlimDX.Result OnResetDevice()
        {
            foreach (Control control in Controls)
            {
                control.OnResetDevice();
            }
            return SlimDX.Result.Last;
        }

        public override void Render()
        {
            // Draw Controls
            if (ScrollbarStyle != Misc.ScrollbarStyle.NONE && Scrollbar != null)
                Scrollbar.RenderClipRect();
            foreach (Control control in Controls)
            {
                control.Render();
            }
            if (ScrollbarStyle != Misc.ScrollbarStyle.NONE && Scrollbar != null)
                Scrollbar.Render();
        }

        public override Size GetMaximumSize()
        {
            return Layout.MaximumLayoutSize(this);

        }
    }
}
