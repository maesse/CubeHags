using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using CubeHags.client.input;
using SlimDX.DirectInput;
using SlimDX;
using System.Windows.Forms;
using CubeHags.client.render;
using CubeHags.client.map.Source;
using CubeHags.client.gfx;
using System.Drawing;
using CubeHags.client.common;

namespace CubeHags.client.gui
{
    sealed class WindowManager : RenderGroup, IResettable
    {
        private static readonly WindowManager _Instance = new WindowManager();
        private List<Window> Windows;
        public bool ShowManager = false;
        public ConnectGUI connectGUI;
        public enum CursorType
        {
            NORMAL=0,
            MOVE,
            SCALE,
            CARET,
            VERT,
            HORIZ
        }

        // Drawing of cursor
        public CursorType Cursor = CursorType.NORMAL;
        Size cursorSize = new Size(32, 32);
        private HagsTexture[] cursorTextures;
        private System.Drawing.Size[] cursor_offsets;
        
        private int nextZvalue = 0; // z-index for windows
        private Dimension nextPosition = new Dimension(100, 100); // position for newly spawned windows

        private Window LastWindowMouseEnter = null;

        // Mouse lock
        public bool MouseLock { get { return _MouseLock; } }
        private bool _MouseLock = false;
        private Window MouseLockWindow = null;

        // Pr frame buffers
        public List<KeyValuePair<ulong, RenderDelegate>> renderCalls = new List<KeyValuePair<ulong, RenderDelegate>>();
        public List<VertexPosTex> VertexList = new List<VertexPosTex>();
        public int nPrimitives;

        // Buffers for rendering
        WindowManager()
        {

            
        }

        public void Init(SlimDX.Direct3D9.Device device)
        {
            Windows = new List<Window>();

            // Bind ESC to ToggleUI
            Commands.Instance.AddCommand("toggleui", new CommandDelegate(ToggleUI));

            // Load cursors
            cursorTextures = new HagsTexture[6];
            cursor_offsets = new System.Drawing.Size[6];


            // textures
            cursorTextures[0] = new HagsTexture("window-theme/cursor.png");
            cursorTextures[1] = new HagsTexture("window-theme/cursor_move.png");
            cursorTextures[2] = new HagsTexture("window-theme/cursor_scale.png");
            cursorTextures[3] = new HagsTexture("window-theme/cursor_caret.png");
            cursorTextures[4] = new HagsTexture("window-theme/cursor_horiz.png");
            cursorTextures[5] = new HagsTexture("window-theme/cursor_vert.png");

            // Set cursor pixel "position" for each cursor theme
            cursor_offsets[0] = new System.Drawing.Size(8, 2);
            cursor_offsets[1] = new System.Drawing.Size(3, 1);
            cursor_offsets[2] = new System.Drawing.Size(3, 2);
            cursor_offsets[3] = new System.Drawing.Size(15, 17);
            cursor_offsets[4] = new System.Drawing.Size(15, 14);
            cursor_offsets[5] = new System.Drawing.Size(14, 16);

            // Hook to input
            Input.Instance.Event += new InputHandler(Input_Event);

            AddWindow(new InfoUI());
            //AddWindow(new MainRibbon());
            vb = new HagsVertexBuffer();
            //AddWindow(new FPSCounter());
            base.Init();
            connectGUI = new ConnectGUI();
            AddWindow(connectGUI);
        }

        void Input_Event(object sender, InputArgs e)
        {
            if (ShowManager)
            {
                foreach (KeyEvent evt in e.args)
                {
                    if (LastWindowMouseEnter != null)
                        LastWindowMouseEnter.KeyDown(evt);
                }
            }
        }

        // Sends all mouse event to a particular window until it releases its lock
        public bool GetMouseLock(Window window)
        {
            // Is the lock free?
            if (!_MouseLock && MouseLockWindow == null)
            {
                MouseLockWindow = window;
                _MouseLock = true;
                return true;
            }
            else
                return false;
        }

        public void ReleaseMouseLock()
        {
            _MouseLock = false;
            MouseLockWindow = null;
        }

        // Add windows to display and positions it according to its needs.
        public void AddWindow(Window window) {
            window.Zvalue = nextZvalue++;
            SetWindowPosition(window);
            window.LayoutUpdate(false);
            Windows.Add(window);
        }

        // Incomming mouse event from Input layer
        public void HandleMouseEvent(MouseEvent evt)
        {
            // Ignore if UI not in focus
            if (!ShowManager)
                return;

            // Special case for windows that has privileged focus over mouse
            if (MouseLock && MouseLockWindow != null)
            {
                // Check button statechange and send events
                if ((evt.Type & MouseEvent.EventType.MOUSEDOWN) > 0)
                {
                    MouseLockWindow.MouseDown(evt);
                    // Send window to front, if necessary
                    if (MouseLockWindow.Zvalue < nextZvalue - 1)
                        MouseLockWindow.Zvalue = nextZvalue++;
                }
                if ((evt.Type & MouseEvent.EventType.MOUSEUP) > 0)
                    MouseLockWindow.MouseUp(evt);
                if ((evt.Type & MouseEvent.EventType.MOVE) > 0 && MouseLockWindow != null)
                    MouseLockWindow.MouseMove(evt);
            }
            // Iterate windows and try to find one that has been hit
            else
            {
                // sort windows - most recent focussed first
                Windows.Sort(new WindowComparer());

                // Iterate over windows untill a window is hit
                bool windowHit = false;
                foreach (Window window in Windows)
                {
                    // Check against bounds
                    if (window.Bound.Contains(evt.Position))
                    {
                        windowHit = true;
                        //window.Update();

                        // Entering new window?
                        if (LastWindowMouseEnter != window)
                        {
                            // Notify old window
                            if (LastWindowMouseEnter != null)
                                LastWindowMouseEnter.MouseExit(evt);
                            LastWindowMouseEnter = window;
                            // Notify new window
                            window.MouseEnter(evt);
                        }

                        // Check button statechange and send events
                        if ((evt.Type & MouseEvent.EventType.MOUSEDOWN) > 0)
                        {
                            window.MouseDown(evt);
                            // Send window to front, if necessary
                            if (window.Zvalue < nextZvalue - 1)
                                window.Zvalue = nextZvalue++;
                        }
                        if ((evt.Type & MouseEvent.EventType.MOUSEUP) > 0)
                            window.MouseUp(evt);
                        if ((evt.Type & MouseEvent.EventType.MOVE) > 0)
                            window.MouseMove(evt);

                        break;
                    }
                }

                // No window hit - show default cursor
                if (!windowHit)
                    Cursor = 0;
            }
        }

        public void Update()
        {
            // Mouse is handled through events now
        }

        // Prepare and send rendercalls and buffers to Renderer
        public void Render()
        {
            // Clear buffers for next frame
            renderCalls.Clear();
            VertexList.Clear();

            // sort back to front
            Windows.Sort(new WindowComparerReversed());
            foreach (Window window in Windows)
            {
                // Render if visible
                if (window.Visible)
                    window.Render();
            }

            // Render Mouse Cursor
            if (ShowManager)
            {
                // Get verts
                int vertexStart = VertexList.Count;
                VertexPosTex[] verts = MiscRender.GetQuadPoints(new System.Drawing.Rectangle(new System.Drawing.Point(Input.Instance.MouseX - cursor_offsets[(int)Cursor].Width, Input.Instance.MouseY - cursor_offsets[(int)Cursor].Height), cursorSize), new Rectangle(Point.Empty, cursorTextures[(int)Cursor].Size), cursorTextures[(int)Cursor].Size);
                VertexList.AddRange(verts);

                // Create rendercall
                ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, cursorTextures[(int)Cursor].MaterialID, 0, 0, vb.VertexBufferID);
                RenderDelegate del = new RenderDelegate((effect, device, setMaterial) => {
                    if (setMaterial)
                        device.SetTexture(0, cursorTextures[(int)Cursor].Texture);

                    device.DrawPrimitives(PrimitiveType.TriangleList, vertexStart, 2);
                });
                renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
            }

            // Send rendercalls to renderer
            if (VertexList.Count > 0)
            {
                vb.SetVB<VertexPosTex>(VertexList.ToArray(), VertexList.Count * VertexPosTex.SizeInBytes, VertexPosTex.Format, Usage.WriteOnly);
                Renderer.Instance.drawCalls.AddRange(renderCalls);
            }

        }

        // Only shows/enables mouse atm
        public void ToggleUI(string[] tokens)
        {
            ShowManager = !ShowManager;
            // Send MouseExit event to focused window
            if (!ShowManager && LastWindowMouseEnter != null)
            {
                LastWindowMouseEnter.MouseExit(new MouseEvent());
            }
        }
        // Decide window position
        public void SetWindowPosition(Window window)
        {
            Dimension pos = new Dimension();

            switch (window.WindowSpawnPosition)
            {
                case Corner.NONE:
                    goto default;
                case Corner.TOPLEFT:
                    window.Position = new Dimension();
                    break;
                case Corner.TOPRIGHT:
                    pos = new Dimension(Renderer.Instance.device.Viewport.Width - window.Bound.Width, 0);
                    window.Position = pos;
                    break;
                case Corner.BOTLEFT:
                    pos = new Dimension(0, Renderer.Instance.device.Viewport.Height - window.Bound.Height);
                    window.Position = pos;
                    break;
                case Corner.BOTRIGHT:
                    pos = new Dimension(Renderer.Instance.device.Viewport.Width - window.Bound.Width, Renderer.Instance.device.Viewport.Height - window.Bound.Height);
                    window.Position = pos;
                    break;
                case Corner.TOP:
                    pos = new Dimension((Renderer.Instance.device.Viewport.Width / 2) - (window.Bound.Width / 2), 0);
                    window.Position = pos;
                    break;
                case Corner.BOT:
                    pos = new Dimension((Renderer.Instance.device.Viewport.Width / 2) - (window.Bound.Width / 2), Renderer.Instance.device.Viewport.Height - window.Bound.Height);
                    window.Position = pos;
                    break;
                case Corner.LEFT:
                    pos = new Dimension(0, (Renderer.Instance.device.Viewport.Height / 2) - (window.Bound.Height / 2));
                    window.Position = pos;
                    break;
                case Corner.RIGHT:
                    pos = new Dimension(Renderer.Instance.device.Viewport.Width - window.Bound.Width, (Renderer.Instance.device.Viewport.Height / 2) - (window.Bound.Height / 2));
                    window.Position = pos;
                    break;
                default:
                    window.Position = nextPosition;
                    // Check if next position exceeds screen
                    if (nextPosition.X + window.Size.Width > Renderer.Instance.device.Viewport.Width)
                    {
                        // reset x
                        nextPosition.X = 70;
                    }
                    if (nextPosition.Y + window.Size.Height > Renderer.Instance.device.Viewport.Height)
                    {
                        // reset Y
                        nextPosition.Y = 70;
                    }
                    nextPosition += new Dimension(30, 30);

                    break;
            }
        }

        public static WindowManager Instance {
            get { return _Instance; }
        }

        //public void Dispose()
        //{

        //}

        public Result OnLostDevice()
        {
            foreach (HagsTexture tex in cursorTextures)
            {
                tex.OnLostDevice();
            }
            foreach (Window win in Windows)
            {
                win.OnLostDevice();
            }

            return Result.Last;
        }

        public Result OnResetDevice()
        {
            foreach (HagsTexture tex in cursorTextures)
            {
                tex.OnResetDevice();
            }
            foreach (Window win in Windows)
            {
                win.OnResetDevice();
            }

            return Result.Last;
        }
    }
}
