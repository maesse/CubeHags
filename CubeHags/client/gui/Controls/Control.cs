using System;
using System.Collections.Generic;
 
using System.Text;
using System.Drawing;
using CubeHags.client.input;
using System.Windows.Forms;
using SlimDX.Direct3D9;

namespace CubeHags.client.gui
{
    /// Base class for all widgets
    public abstract class Control : IResettable
    {

        public static int MAXSIZE = 5000; // max height/width of controls
        public Window Window; // window control is part of
        public Container parent;

        public string Name = "";
        public bool Visible = true;
        
        public bool HasFocus;
        public bool Enabled = true;
        //public bool CanFocus;

        //public SlimDX.Direct3D9.Font Font { get { if (_Font == null) InitFont(); return _Font; } set { _Font = value; } }
        //private SlimDX.Direct3D9.Font _Font = null;
        public System.Drawing.Color Color = Color.White;
        public int Margin = 0; // margin from text
        
        // Size & Position
        public Dimension Position = new Dimension();
        public Size Size = new Size();
        public Rectangle Bound = new Rectangle();
        
        // Layout sizes
        public Size PreferredSize = new Size();
        public Size MinSize = new Size();
        public Size MaxSize = new Size(MAXSIZE, MAXSIZE);

        public Control(Window window)
        {
            this.Window = window;
            MouseDown = new ControlEvents.MouseDown(MouseDownEvent);
            MouseMove = new ControlEvents.MouseMove(MouseMoveEvent);
            MouseUp = new ControlEvents.MouseUp(MouseUpEvent);
            MouseEnter = new ControlEvents.MouseEnter(MouseEnterEvent);
            MouseExit = new ControlEvents.MouseExit(MouseExitEvent);
            KeyDown = new ControlEvents.KeyDown(KeyDownEvent);
        }

        public abstract void Render();
        public virtual Size GetPreferredSize() { return PreferredSize; }
        public virtual Size GetMinimumSize() { return MinSize; }
        public virtual Size GetMaximumSize() { return MaxSize; }

        // Events
        //public abstract void FireMouseEvent(MouseEvent evt); // deprecated
        public ControlEvents.MouseDown MouseDown = null;
        public ControlEvents.MouseMove MouseMove = null;
        public ControlEvents.MouseUp MouseUp = null;
        public ControlEvents.MouseEnter MouseEnter = null;
        public ControlEvents.MouseExit MouseExit = null;
        public ControlEvents.KeyDown KeyDown = null;

        // Default event methods.. useful for override
        public virtual void MouseDownEvent(MouseEvent evt) {}
        public virtual void MouseUpEvent(MouseEvent evt) { }
        public virtual void MouseMoveEvent(MouseEvent evt) { }
        public virtual void MouseEnterEvent(MouseEvent evt) { }
        public virtual void MouseExitEvent(MouseEvent evt) { }
        public virtual void KeyDownEvent(KeyEvent evt) { }

        public virtual SlimDX.Result OnLostDevice() { return SlimDX.Result.Last; }
        public virtual SlimDX.Result OnResetDevice() { return SlimDX.Result.Last; }
    }

    // Event delegate definition
    public class ControlEvents
    {
        public delegate void MouseDown(MouseEvent evt);
        public delegate void MouseMove(MouseEvent evt);
        public delegate void MouseUp(MouseEvent evt);
        public delegate void MouseEnter(MouseEvent evt);
        public delegate void MouseExit(MouseEvent evt);
        public delegate void KeyDown(KeyEvent evt);
    }
}
