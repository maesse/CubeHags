using System;
using System.Collections.Generic;

using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;
using System.Windows.Forms;
using CubeHags.client.gfx;
using CubeHags.client.render;

namespace CubeHags.client.gui
{
    class TextBox : Control
    {
        // Theming
        HagsAtlas atlas;

        // Text
        private string _Text = "";
        public string Text { get { return _Text; } set { if (value != _Text) Changed = true; _Text = value; } }
        private Rectangle TextSize;
        public bool Changed = true;
        private Rectangle DrawRect;
        Rectangle oldrect = Rectangle.Empty;
        public int maxChars = 255;

        // Overflow
        public bool AllowOverflow = true; // Allow more text than can be shown
        private int OverflowOffset = 0; // Pixels to offset label drawing

        // Caret
        private Texture WhiteTexture;
        public bool DrawCaret = true;
        private int _CaretPosition;
        private int CaretPosition { get { return _CaretPosition; } set { _CaretPosition = value; CarretPositionChanged(); } }
        private PointF CaretPositionF = new PointF();

        // Selection
        private bool Selection = false;
        private int SelectStart = 0, SelectEnd = 0;

        // Single Charater Size
        private Rectangle ASize = new Rectangle(0, 0, 1, 1);

        public TextBox(Window window)
            : base(window)
        {
            PreferredSize = new Size(240, 26);


            atlas = new HagsAtlas("window-theme/textframe.png");

            //HagsAtlas.SerializeToXML(atlas);
            atlas["topleft"] = new Rectangle(0, 0, 3, 3);
            atlas["top"] = new Rectangle(5, 0, 3, 3);
            atlas["topright"] = new Rectangle(28, 0, 3, 3);

            atlas["left"] = new Rectangle(0, 5, 3, 3);
            atlas["middle"] = new Rectangle(5, 5, 3, 3);
            atlas["right"] = new Rectangle(28, 5, 3, 3);

            atlas["bottomleft"] = new Rectangle(0, 28, 3, 3);
            atlas["bottom"] = new Rectangle(5, 28, 3, 3);
            atlas["bottomright"] = new Rectangle(28, 28, 3, 3);

            // Load theme
            WhiteTexture = TextureManager.CreateTexture(1, 1, Format.A8R8G8B8, new SlimDX.Color4(Color.White));
            //WhiteTexture = TextureManager.Instance.LoadTexture("window-theme/White.bmp");
            // Fixed sized font measurement
            ASize = new Rectangle();
            Renderer.Instance.Fonts["textbox"].MeasureString(Renderer.Instance.sprite, "A", DrawTextFormat.Left, ref ASize);

            // Little prefferedsize tweak
            int goodHeight = ASize.Height + atlas["top"].Height + atlas["bottom"].Height;
            if (goodHeight > PreferredSize.Height)
            {
                PreferredSize.Height = goodHeight;
            }
        }

        public void CarretPositionChanged()
        {
            int realCaretPosition = CaretPosition * ASize.Width;

            // Caret positioned out of view?
            if (realCaretPosition + OverflowOffset >= Size.Width - atlas["left"].Width - atlas["right"].Width)
            {
                OverflowOffset = -(realCaretPosition - Size.Width + atlas["left"].Width + atlas["right"].Width + 1);
            }
            else if (OverflowOffset < 0 && realCaretPosition + OverflowOffset < atlas["left"].Width)
            {
                OverflowOffset = -(realCaretPosition);
            }
            //else if (OverflowOffset > 0)
            //{
            //    OverflowOffset = 0;
            //}

            CaretPositionF = new PointF(realCaretPosition + OverflowOffset, 0f);
        }

        public override void MouseDownEvent(CubeHags.client.input.MouseEvent evt)
        {
            // Grab focus
            Window.FocusControl = this;
            Window.GetMouseLock(this);

            // Set Caret Position
            Point pt = CharPositionFromPoint(new Point(evt.Position.X, evt.Position.Y));

            // Bounds checking
            if (pt.X > _Text.Length)
                pt.X = _Text.Length;

            // Append to selection if shift button is held down
            if ((Input.Instance.KeyModifiers & KeyEvent.Modifiers.SHIFT) > 0)
            {
                if (!Selection)
                {
                    SelectStart = CaretPosition;
                    Selection = true;
                }
                SelectEnd = pt.X;
            }
            else // else Start new selection
            {
                SelectStart = SelectEnd = pt.X;
                Selection = true;
            }

            CaretPosition = pt.X;
        }

        public override void MouseUpEvent(CubeHags.client.input.MouseEvent evt)
        {
            Window.ReleaseMouseLock();
            // Stop selection if nothing is selected
            if (Math.Abs(SelectStart - SelectEnd) == 0)
                Selection = false;
        }

        public override void MouseMoveEvent(CubeHags.client.input.MouseEvent evt)
        {
            WindowManager.Instance.Cursor = WindowManager.CursorType.CARET;

            // Mouse button0 drag + selection
            if (evt.ButtonState[0] && Selection)
            {
                Point pt = CharPositionFromPoint(new Point(evt.Position.X, evt.Position.Y));
                // If position changed and within bounds..
                if (CaretPosition != pt.X && pt.X <= _Text.Length)
                {
                    SelectEnd = CaretPosition = pt.X;
                }
            }
        }

        public override void MouseExitEvent(CubeHags.client.input.MouseEvent evt)
        {
            WindowManager.Instance.Cursor = WindowManager.CursorType.NORMAL;
        }



        public override void Render()
        {
            // Update text size
            if (Changed)
            {
                TextSize = new Rectangle();
                Renderer.Instance.Fonts["textbox"].MeasureString(Renderer.Instance.sprite, Text, DrawTextFormat.Left, ref TextSize);

                Changed = false;
            }
            DrawRect = new Rectangle(Position.X + atlas["left"].Width, Position.Y + atlas["top"].Height, Size.Width - atlas["left"].Width - atlas["left"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height);

            // Early out
            if (DrawRect.Width <= 0)
                return;

            int vertStartOffset = WindowManager.Instance.VertexList.Count;
            // Draw borders
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X, Position.Y), atlas["topleft"].Size), atlas["topleft"], atlas["topleft"].Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + atlas["topleft"].Width, Position.Y), new SizeF(Size.Width - atlas["topleft"].Width - atlas["topright"].Width, atlas["top"].Height)), atlas["top"], atlas["top"].Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + Size.Width - atlas["topright"].Width, Position.Y), atlas["topright"].Size), atlas["topright"], atlas["topright"].Size));

            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X, Position.Y + atlas["topleft"].Height), new SizeF(atlas["left"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height)), atlas["left"], atlas["left"].Size));

            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + Size.Width - atlas["right"].Width, Position.Y + atlas["topright"].Height), new SizeF(atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height)), atlas["right"], atlas["right"].Size));

            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X, Position.Y + Size.Height - atlas["bottomleft"].Height), new SizeF()), atlas["bottomleft"], atlas["bottomleft"].Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + atlas["bottomleft"].Width, Position.Y + Size.Height - atlas["bottom"].Height), new SizeF(Size.Width - atlas["bottomleft"].Width - atlas["bottomright"].Width, atlas["bottom"].Height)), atlas["bottom"], atlas["bottom"].Size));
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + Size.Width - atlas["bottomright"].Width, Position.Y + Size.Height - atlas["bottomright"].Height), new SizeF()), atlas["bottomright"], atlas["bottomright"].Size));
            int nPrimitives = (WindowManager.Instance.VertexList.Count - vertStartOffset) / 3;

            int vertStartOffset2 = WindowManager.Instance.VertexList.Count;
            WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + atlas["left"].Width, Position.Y + atlas["top"].Height), new SizeF(Size.Width - atlas["left"].Width - atlas["right"].Width, Size.Height - atlas["top"].Height - atlas["bottom"].Height)), atlas["middle"], atlas["middle"].Size, new SlimDX.Color4((Enabled ? 0.2f : 0.4f), 1f, 1f, 1f)));
            if (Selection)
            {
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + (Math.Min(SelectStart, SelectEnd) * ASize.Width + atlas["left"].Width) + OverflowOffset, Position.Y + atlas["top"].Height), new SizeF(ASize.Width * (Math.Abs(SelectStart - SelectEnd)), ASize.Height)), new SlimDX.Color4(0.5f, (173f / 255f), (216f / 255f), (230f / 255f))));
            }
            if (DrawCaret && HasFocus)
            {
                WindowManager.Instance.VertexList.AddRange(MiscRender.GetQuadPoints(new RectangleF(new PointF(Position.X + CaretPositionF.X + atlas["left"].Width, Position.Y + CaretPositionF.Y + atlas["top"].Height), new SizeF(1, Size.Height - 6)), new SlimDX.Color4(Enabled ? 0.8f : 0.4f, 1f, 1f, 1f)));
            }

            int nPrimitives2 = (WindowManager.Instance.VertexList.Count - vertStartOffset2) / 3;

            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, atlas.Texture.MaterialID, 0, 0, WindowManager.Instance.vb.VertexBufferID);
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                //if (Renderer.Instance.CanScissor)
                {
                    oldrect = device.ScissorRect;
                    device.ScissorRect = DrawRect;
                }

                device.SetTexture(0, atlas.Texture.Texture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset, nPrimitives);

                Renderer.Instance.sprite.Begin(SpriteFlags.AlphaBlend);
                Renderer.Instance.Fonts["textbox"].DrawString(Renderer.Instance.sprite, _Text, new Rectangle(new Point(Position.X + atlas["left"].Width + OverflowOffset, Position.Y + atlas["top"].Height + 2), new Size(TextSize.Width, Size.Height - atlas["top"].Height - atlas["bottomleft"].Height)), DrawTextFormat.Left, (Enabled ? Color : Color.FromArgb(128, Color)));
                Renderer.Instance.sprite.End();

                device.SetTexture(0, WhiteTexture);
                device.DrawPrimitives(PrimitiveType.TriangleList, vertStartOffset2, nPrimitives2);

                //if (Renderer.Instance.CanScissor)
                {
                    device.ScissorRect = oldrect;
                }
            });

            WindowManager.Instance.renderCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }

        private void RemoveSelection()
        {
            if (Selection)
            {
                Text = Text.Remove(Math.Min(SelectStart, SelectEnd), Math.Abs(SelectEnd - SelectStart));
                CaretPosition = Math.Min(SelectStart, SelectEnd);
                if (CaretPosition > Text.Length)
                    CaretPosition = Text.Length;
                Selection = false;
            }
        }

        public override void KeyDownEvent(KeyEvent evt)
        {
            if (Enabled)
            {
                // Append char to text
                if (!evt.IsUpDownEvent)
                {
                    char c = evt.Character;// Input.KeyToChar(evt.key, evt.Mod);
                    if (c >= 32 && c <= 126)
                    {

                        // Check for disallowed overflow
                        if (AllowOverflow ||
                            (!AllowOverflow && (Text.Length + 1) * ASize.Width <= Size.Width - atlas["left"].Width - atlas["right"].Width))
                        {
                            RemoveSelection();
                            if (Text.Length == maxChars)
                                return;
                            if (CaretPosition == Text.Length)
                                Text += c;
                            else if (CaretPosition == 0)
                                Text = c + Text;
                            else
                                Text = Text.Insert(CaretPosition, "" + c);

                            CaretPosition++;
                        }

                        if (Selection && (evt.Mod & KeyEvent.Modifiers.SHIFT) == 0)
                        {
                            Selection = false;
                        }
                    }
                    else // Not a char
                    {
                        // handle special keys
                        int charint = (int)c;
                        switch ((int)c)
                        {
                            case 8:
                                // Remove selection or remove one char
                                if (CaretPosition > 0)
                                {
                                    if (Selection)
                                        RemoveSelection();
                                    else
                                        Text = Text.Remove(--CaretPosition, 1);
                                }

                                break;
                            //case System.Windows.Input.Key.Left:
                            //    if (CaretPosition > 0)
                            //    {
                            //        CheckForShift(evt);
                            //        CaretPosition--;
                            //    }

                            //    break;
                            //case System.Windows.Input.Key.Right:
                            //    if (CaretPosition < Text.Length)
                            //    {
                            //        CheckForShift(evt);
                            //        CaretPosition++;
                            //    }

                            //    break;
                            //case System.Windows.Input.Key.Delete:
                            //    if (Selection)
                            //        RemoveSelection();
                            //    else if (CaretPosition < Text.Length)
                            //        Text = Text.Remove(CaretPosition, 1);

                            //    break;
                            //case System.Windows.Input.Key.Home:
                            //    CheckForShift(evt);
                            //    CaretPosition = 0;

                            //    break;
                            //case System.Windows.Input.Key.End:
                            //    CheckForShift(evt);
                            //    CaretPosition = Text.Length;

                            //    break;
                        }
                    }

                    if (Selection && (evt.Mod & KeyEvent.Modifiers.SHIFT) > 0)
                    {
                        SelectEnd = CaretPosition;
                    }
                }
            }
        }

        // Checks if shift button is held down and manages selection accordingly
        private void CheckForShift(KeyEvent evt)
        {
            // Check for shift..Select text
            if ((evt.Mod & KeyEvent.Modifiers.SHIFT) > 0)
            {
                if (!Selection)
                {
                    Selection = true;
                    SelectStart = SelectEnd = CaretPosition;
                }
            }
            else if (Selection)
            {
                Selection = false;
            }
        }

        // From mouse point to string position
        public Point CharPositionFromPoint(Point pt)
        {
            Point RenderPoint = new Point(Position.X + atlas["left"].Width + OverflowOffset, Position.Y + atlas["top"].Height);

            // Relative to Render area + a little usability tweak for selecting
            int dx = pt.X - RenderPoint.X + (ASize.Width / 2) - 1;
            int dy = pt.Y - RenderPoint.Y + (ASize.Height / 2) - 1;

            // Divide by fonts fixed size to get position
            int charx = dx / ASize.Width;
            int chary = dy / ASize.Height;
            System.Console.WriteLine("MouseDown on charx: " + charx);

            // Bounds limiting
            if (charx < 0)
                charx = 0;
            else if (charx > Text.Length)
                charx = Text.Length;

            return new Point(charx, chary);
        }

        public override SlimDX.Result OnLostDevice()
        {
            WhiteTexture.Dispose();
            return base.OnLostDevice();
        }

        public override SlimDX.Result OnResetDevice()
        {
            WhiteTexture = TextureManager.CreateTexture(1, 1, Format.A8R8G8B8, new SlimDX.Color4(Color.White));
            return base.OnResetDevice();
        }

    }
}
