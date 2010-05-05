using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;
using System.Windows.Forms;

namespace CubeHags.client.gui.Controls
{
    class TextBox : Control
    {
        // Theming
        private Texture textboxTexture;
        private Rectangle[] regions;

        // Text
        private string _Text = "";
        public string Text { get { return _Text; } set { if (value != _Text) Changed = true; _Text = value; } }
        private Rectangle TextSize;
        public bool Changed = true;
        private Rectangle DrawRect;

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
        private Rectangle ASize = new Rectangle(0,0,1,1);

        public TextBox(Window window)
            : base(window)
        {
            PreferredSize = new Size(100,22);

            // Load theme
            //textboxTexture = TextureManager.Instance.LoadTexture("window-theme/textframe.png");
            //WhiteTexture = TextureManager.Instance.LoadTexture("window-theme/White.bmp");
            // Define theme corners
            regions = new Rectangle[9];
            regions[0] = new Rectangle(0, 0, 3, 3);
            regions[1] = new Rectangle(5, 0, 3, 3);
            regions[2] = new Rectangle(28, 0, 3, 3);
            regions[3] = new Rectangle(0, 5, 3, 3);
            regions[5] = new Rectangle(28, 5, 3, 3);
            regions[6] = new Rectangle(0, 28, 3, 3);
            regions[7] = new Rectangle(5, 28, 3, 3);
            regions[8] = new Rectangle(28, 28, 3, 3);

            // Fixed sized font measurement
            ASize = new Rectangle();
            Renderer.Instance.Fonts["textbox"].MeasureString(Renderer.Instance.sprite, "A", DrawTextFormat.Left, ref ASize);

            // Little prefferedsize tweak
            int goodHeight = ASize.Height + regions[1].Height + regions[7].Height;
            if (goodHeight > PreferredSize.Height)
            {
                PreferredSize.Height = goodHeight;
            }
        }

        public void CarretPositionChanged()
        {
            int realCaretPosition = CaretPosition * ASize.Width;

            // Caret positioned out of view?
            if (realCaretPosition + OverflowOffset >= Size.Width - regions[3].Width - regions[5].Width)
            {
                OverflowOffset = -(realCaretPosition -  Size.Width + regions[3].Width + regions[5].Width + 1);
            } 
            else if(OverflowOffset < 0 && realCaretPosition + OverflowOffset < regions[3].Width)
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
            if(Math.Abs(SelectStart - SelectEnd) == 0)
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
                DrawRect = new Rectangle(Bound.X + regions[3].Width, Bound.Y + regions[1].Height, Bound.Width - regions[3].Width - regions[3].Width, Bound.Height - regions[1].Height - regions[7].Height);
                Changed = false;
            }

            // Draw borders
            //sprite.Begin(SpriteFlags.AlphaBlend);
            //sprite.Draw2D(textboxTexture, regions[0], regions[0].Size, new PointF(Position.X, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(textboxTexture, regions[1], new SizeF(Size.Width - regions[0].Width - regions[2].Width, regions[1].Height), new PointF(Position.X + regions[0].Width, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(textboxTexture, regions[2], regions[2].Size, new PointF(Position.X + Size.Width - regions[3].Width, Position.Y), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //sprite.Draw2D(textboxTexture, regions[3], new SizeF(regions[3].Width, Size.Height - regions[1].Height - regions[7].Height), new PointF(Position.X, Position.Y + regions[0].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(WhiteTexture, new Rectangle(0,0,2,2), new SizeF(Size.Width - regions[3].Width - regions[5].Width, Size.Height - regions[1].Height - regions[7].Height), new PointF(Position.X + regions[3].Width, Position.Y + regions[1].Height), Color.FromArgb((Enabled? 0: 25), Color.White));
            //sprite.Draw2D(textboxTexture, regions[5], new SizeF(regions[5].Width, Size.Height - regions[1].Height - regions[7].Height), new PointF(Position.X + Size.Width - regions[5].Width, Position.Y + regions[2].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));

            //sprite.Draw2D(textboxTexture, regions[6], regions[6].Size, new PointF(Position.X, Position.Y + Size.Height - regions[6].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(textboxTexture, regions[7], new SizeF(Size.Width - regions[6].Width - regions[8].Width, regions[7].Height), new PointF(Position.X + regions[6].Width, Position.Y + Size.Height - regions[7].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.Draw2D(textboxTexture, regions[8], regions[8].Size, new PointF(Position.X + Size.Width - regions[8].Width, Position.Y + Size.Height - regions[8].Height), Color.FromArgb((int)(Window.Opacity * 255), Color.White));
            //sprite.End();
            // Start clipping
            Renderer.Instance.device.SetRenderState(RenderState.ScissorTestEnable, true);
            Renderer.Instance.device.ScissorRect = DrawRect;
            // Draw text
            Renderer.Instance.sprite.Begin(SpriteFlags.AlphaBlend);
            //Font.DrawText(textSprite, _Text, new Rectangle(new Point(Position.X + regions[3].Width + OverflowOffset, Position.Y + regions[1].Height), new Size(Size.Width - regions[3].Width - regions[5].Width, Size.Height - regions[1].Height - regions[6].Height)), DrawTextFormat.None, (Enabled? Color: Color.FromArgb(128, Color)));
            Renderer.Instance.Fonts["textbox"].DrawString(Renderer.Instance.sprite, _Text, new Rectangle(new Point(Position.X + regions[3].Width + OverflowOffset, Position.Y + regions[1].Height), new Size(TextSize.Width, Size.Height - regions[1].Height - regions[6].Height)), DrawTextFormat.Left, (Enabled ? Color : Color.FromArgb(128, Color)));
            Renderer.Instance.sprite.End();

            // Draw Caret and selection
            //sprite.Begin(SpriteFlags.AlphaBlend);
            //if (Selection)
            //{
            //    sprite.Draw2D(WhiteTexture, new Rectangle(0, 0, 2, 2), new SizeF(ASize.Width * (Math.Abs(SelectStart - SelectEnd)), ASize.Height), new PointF(Position.X + (Math.Min(SelectStart, SelectEnd) * ASize.Width + regions[3].Width) + OverflowOffset, Position.Y + regions[1].Height), Color.FromArgb(128, Color.LightBlue));
            //}
            //if (DrawCaret && HasFocus)
            //{
            //    sprite.Draw2D(WhiteTexture, new Rectangle(0, 0, 2, 2), new SizeF(1, Size.Height - 6), new PointF(Position.X + CaretPositionF.X + regions[3].Width, Position.Y + CaretPositionF.Y + regions[1].Height), Color.FromArgb((Enabled? 200: 100), Color.White));
            //}
            //sprite.End();

            // End clipping
            Renderer.Instance.device.SetRenderState(RenderState.ScissorTestEnable, false);
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
                if (evt.pressed)
                {
                    char? c = '\0';// Input.KeyToChar(evt.key, evt.Mod);
                    if (c.HasValue)
                    {
                        // Check for disallowed overflow
                        if (AllowOverflow || 
                            (!AllowOverflow && (Text.Length+1) * ASize.Width <= Size.Width - regions[3].Width - regions[5].Width))
                        {
                            RemoveSelection();

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
                        switch (evt.key)
                        {
                            //case System.Windows.Input.Key.Back:
                            //    // Remove selection or remove one char
                            //    if (CaretPosition > 0)
                            //    {
                            //        if (Selection)
                            //            RemoveSelection();
                            //        else
                            //            Text = Text.Remove(--CaretPosition, 1);
                            //    }

                            //    break;
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
                            //    else if(CaretPosition < Text.Length)
                            //            Text = Text.Remove(CaretPosition, 1);

                            //    break;
                            //case System.Windows.Input.Key.Home:
                            //    CheckForShift(evt);
                            //    CaretPosition = 0;

                            //    break;
                            //case System.Windows.Input.Key.End:
                            //    CheckForShift(evt);
                            //    CaretPosition = Text.Length;

                                //break;
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
            Point RenderPoint = new Point(Position.X + regions[3].Width + OverflowOffset, Position.Y + regions[1].Height);

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
    }
}
