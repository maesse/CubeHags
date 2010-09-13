using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.client.gfx;
using SlimDX;
using CubeHags.client.map.Source;
using CubeHags.common;
using CubeHags.client.render;
using System.Drawing;
using CubeHags.client.render.Formats;
using SlimDX.Direct3D9;
using System.Windows.Forms;
using CubeHags.client.common;

namespace CubeHags.client.gui
{
    public class Letter
    {
        public int Id;
        public Vector2 TexCoord;

        public Letter(int id, Vector2 texcoord)
        {
            Id = id;
            this.TexCoord = texcoord;
        }
    }

    public sealed class HagsConsole
    {
        private static readonly HagsConsole _Instance = new HagsConsole();
        public static HagsConsole Instance { get { return _Instance; } }

        bool Visible = false;
        public bool IsVisible { get { return Visible; } }

        Letter[] letters = null;
        HagsTexture font;
        HagsTexture bg;

        List<string> lines = new List<string>(); // log

        string currentText = ""; // commandline text
        int currentPos; // caret position
        List<string> commandHistory = new List<string>();
        int currentHistory = -1; // Current history-id we are viewing

        public string LinePrefix = "$ "; // commandline prefix
        Color4[] ConsoleColors = new Color4[] {
            Color.Black,
            Color.Red,
            Color.Green,
            Color.Yellow,
            Color.Blue,
            Color.Cyan,
            Color.Pink,
            Color.White,
            Color.Gray
        };

        HagsConsole()
        {
        }

        public void Init()
        {
            // Load textures
            font = new HagsTexture("Terminus.png");
            bg = new HagsTexture("consolebg.png");

            // Build vertices for all letters
            int fontw = 9, fonth = 15;
            int hfonts = font.Size.Width / fontw;
            int vfonts = font.Size.Height / fonth;
            int nLetters = hfonts * vfonts; // total number of letters in bitmap font

            letters = new Letter[nLetters];
            int i = 0;
            int currX = 0, currY = 0;
            for (i = 0; i < nLetters; i++)
            {
                // calc coord position
                Vector2 ofs = Vector2.Zero;
                ofs.X = (currX * fontw);
                ofs.Y = (currY * fonth);

                // Control offsets
                currX++;
                if (currX >= hfonts)
                {
                    currX = 0;
                    currY++;
                    if (currY > vfonts)
                        break;
                }

                // Add it in
                Letter letter = new Letter(i, ofs);
                letters[i] = letter;
            }
            if (i != nLetters)
                Common.Instance.WriteLine("Warning: Couldn't load all letters for console");

            Input.Instance.Event += new InputHandler(GotKeyEvent);
        }

        void GotKeyEvent(object sender, InputArgs e) 
        {
            for (int i = 0; i < e.args.Count; i++)
            {
                if (e.args[i].IsUpDownEvent)
                    HandleOtherKey(e.args[i]); // Special keys
                else
                    AddChar(e.args[i].Character); // Character keys
            }
        }

        void HandleOtherKey(KeyEvent evt)
        {
            // Only react on presses
            if (!evt.pressed)
                return;

            switch (evt.key)
            {
                case Keys.Right:
                    HagsConsole.Instance.MoveCaret(true);
                    break;
                case Keys.Left:
                    HagsConsole.Instance.MoveCaret(false);
                    break;
                case Keys.Enter:
                    HagsConsole.Instance.ExecuteLine();
                    break;
                case Keys.Up:
                    HagsConsole.Instance.MoveHistory(true);
                    break;
                case Keys.Down:
                    HagsConsole.Instance.MoveHistory(false);
                    break;
            }
        }

        public int GetCurrentLenght()
        {
            int colors = 0;
            for (int i = 0; i < currentText.Length-1; i++)
            {
                if (currentText[i] == '^' && currentText[i+1] >= '0' && currentText[i+1] <= '9')
                {
                    colors++;
                    i++;
                }
            }
            return currentText.Length + colors * 2;
        }

        // backspace handling
        public void RemoveChar() 
        {
            if (currentPos == 0)
            {
                // do nothing
            }
            else if (currentPos == 1)
            {
                currentText = currentText.Substring(1);
                currentPos--;
            }
            else if (currentPos == GetCurrentLenght())
            {
                currentText = currentText.Substring(0, currentText.Length - 1);
                currentPos--;
            }
            else
            {
                string temp = currentText.Substring(0, currentPos - 1);
                currentText = temp + currentText.Substring(currentPos);
                currentPos--;
            }
        }

        // handles up/down-arrow keypresses
        public void MoveHistory(bool backward)
        {
            if (backward)
            {
                if (currentHistory == -1)
                    currentHistory = commandHistory.Count - 1;
                else if(currentHistory > 0)
                    currentHistory--;
            }
            else if(currentHistory != -1)
            {
                currentHistory++;
                if (currentHistory >= commandHistory.Count)
                    currentHistory = -1;
            }

            // Set caret at the end of the new command
            if (currentHistory == -1)
                currentPos = GetCurrentLenght();
            else
                currentPos = commandHistory[currentHistory].Length;
        }

        // Handles left/right-arrow keypresses
        public void MoveCaret(bool forward)
        {
            if (forward)
                currentPos++;
            else
                currentPos--;

            // Ensure it doesn't go where it shouldn't
            if (currentPos < 0)
                currentPos = 0;
            string text = (currentHistory == -1) ? currentText : commandHistory[currentHistory];
            if (currentPos > text.Length)
                currentPos = text.Length;
        }

        // eg. when enter key is pressed
        public void ExecuteLine()
        {
            if (currentHistory != -1)
                currentText = commandHistory[currentHistory]; // execute history command
            else if (GetCurrentLenght() == 0)
                return;

            commandHistory.Add(currentText);
            string command = currentText;
            ClearLine();

            Common.Instance.WriteLine(LinePrefix + command);
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, command);
        }

        // Clear the current commandline
        public void ClearLine()
        {
            currentText = "";
            currentPos = 0;
            currentHistory = -1;
        }

        // Handles keypresses from keyboard
        public void AddChar(char c)
        {
            if (c == 189) // console char
                return;

            if (currentHistory != -1)
            {
                // Editing history, copy it to currentText
                currentText = commandHistory[currentHistory];
                currentHistory = -1;
            }
            // Check if valid char
            if ((int)c < 32 || (int)c > 32 + letters.Length)
            {
                // Check for special chars
                int cint = (int)c;
                switch (cint)
                {
                    case 8: // backspace
                        RemoveChar();
                        break;
                    case 9: // TAB
                        for (int i = 0; i < 4; i++)
                        {
                            AddChar(' ');
                        }
                        break;

                }
                return;
            }

            if (currentPos == GetCurrentLenght())
            {
                currentText += c;
                currentPos++;
            }
            else if (currentPos == 0)
            {
                currentText = c + currentText;
            }
            else
            {
                string temp = currentText.Substring(0, currentPos);
                currentText = temp + c + currentText.Substring(currentPos);
                currentPos++;
            }
        }

        public static string StripColors(string str)
        {
            if(!str.Contains("^"))
                return str;

            int colorpos = 0;
            StringBuilder strBuilder = new StringBuilder(str.Length);
            int lastpos = 0;
            while((colorpos = str.IndexOf('^', colorpos)) != -1)
            {
                if (str[colorpos + 1] >= '0' && str[colorpos + 1] <= '9')
                {
                    strBuilder.Append(str.Substring(lastpos, colorpos - lastpos));
                    lastpos = colorpos+2;
                    colorpos++;
                }
                else
                    continue;
            }
            strBuilder.Append(str.Substring(lastpos, str.Length - lastpos));
            return strBuilder.ToString();
        }

        public void AddLine(string text)
        {
            if(lines != null)
                lines.Add(text);
        }

        public void ToggleConsole()
        {
            Visible = !Visible;
        }

        // Returns number of lines this string will be breaked up into, on the screen
        int NumLinesForString(string text)
        {
            int lineSize = (Renderer.Instance.RenderSize.Width / 9);
            if (!text.Contains("\n"))
            {
                return text.Length / lineSize;
            }

            int minusLine = (text.EndsWith("\n") ? 1 : 0);

            int nLines = 0;
            int pos = 0;
            while (true)
            {
                int nextNL = text.IndexOf('\n', pos);
                if (nextNL == -1)
                {
                    if ((text.Length - pos) / lineSize > 0) // no new line, but text > linesize
                        nLines += (text.Length - pos) / lineSize;
                    return nLines - minusLine; // finished
                }
                else if (nextNL != -1 && (nextNL - pos) / lineSize == 0) // New line
                    nLines++;
                else if(nextNL != -1 && (nextNL - pos) / lineSize > 0) // New line + new lines from textLength > lineSize
                    nLines += ((nextNL - pos) / lineSize)+1;
                pos = nextNL+1;

            }
        }

        public void Render()
        {
            if (!Visible || letters == null)
                return;

            // LocalServer gives "root" prefix
            if (CVars.Instance.FindVar("sv_running").Integer == 1)
                LinePrefix = "# ";

            List<VertexPositionColorTex> verts = new List<VertexPositionColorTex>();
            VertexPositionColorTex[] bgverts = MiscRender.GetQuadPoints(new System.Drawing.RectangleF(0, 0, Renderer.Instance.RenderSize.Width, Renderer.Instance.RenderSize.Height), new Color4(0.75f, 0.0f, 0.0f, 0.0f));

            verts.AddRange(bgverts);
            // Add background
            int maxLines = Renderer.Instance.RenderSize.Height / 15;
            int maxWidth = Renderer.Instance.RenderSize.Width / 9;

            // Build vert list
            Size screenSize = Renderer.Instance.RenderSize;

            int currH = 0;
            string text = "";
            int textLines = 0;
            Color4 currentColor = Color.White;
            // Start from newest line, to oldest, or to we get to text that cant be seen
            for (int line = lines.Count; line >= 0 && currH <= maxLines; line--)
            {
                int internalH = 0, currW = 0;
                if (line == lines.Count)
                {
                    text = LinePrefix + (currentHistory == -1 ? currentText : commandHistory[currentHistory]);
                    textLines = 1;
                }
                else
                {
                    currentColor = Color.White;
                    text = lines[line];
                    textLines = NumLinesForString(text) + 1;
                }
                currH += textLines;
                
                // Iterate over chars
                for (int i = 0; i < text.Length; i++)
                {
                    int letter = (int)text[i]-32; // Dont have the first 32 ascii chars
                    if (text[i] == '\n')
                    {
                        currW = 0;
                        internalH++;
                        continue;
                    }
                    else if (text[i] == '\r')
                        continue;
                    else if (text.Length > i+1 && text[i] == '^' && text[i + 1] >= '0' && text[i + 1] <= '9') // ^<0-9>
                    {
                        // Change color
                        int colorNumber = int.Parse(text[i + 1].ToString());
                        if (colorNumber < ConsoleColors.Length)
                            currentColor = ConsoleColors[colorNumber];
                        else
                            currentColor = ConsoleColors[7]; // white
                        
                        // Don't draw "^1" in console output
                        if (line != lines.Count)
                        {
                            i++;
                            continue;
                        }
                    }

                    if (letter > letters.Length || letter < 0)
                    {
                        //Common.Instance.WriteLine("Letter out of bounds: " + text[i]);
                        letter = (int)'~' -31;
                    }

                    VertexPositionColorTex[] qv = MiscRender.GetQuadPoints(new System.Drawing.RectangleF(currW * 9, Renderer.Instance.RenderSize.Height - (currH * 15) + (internalH*15), 9, 15), new RectangleF(new PointF(letters[letter].TexCoord.X, letters[letter].TexCoord.Y), new SizeF(9, 15)), font.Size, currentColor);
                    verts.AddRange(qv);
                    
                    // count up the current chars shown
                    currW++;
                    if (currW >= maxWidth)
                    {
                        currW = 0;
                        internalH++;

                        if (internalH >= maxLines)
                            break;
                    }
                }
            }

            // Add caret
            int caretletter = (int)'_'-32;
            verts.AddRange(MiscRender.GetQuadPoints(new System.Drawing.RectangleF((currentPos+2) * 9, Renderer.Instance.RenderSize.Height - 15, 9, 15), new RectangleF(new PointF(letters[caretletter].TexCoord.X, letters[caretletter].TexCoord.Y), new SizeF(9, 15)), font.Size));

            // Pack it up and send it off to the renderer
            VertexPositionColorTex[] arr = verts.ToArray();
            ulong key = SortItem.GenerateBits(SortItem.FSLayer.HUD, SortItem.Viewport.DYNAMIC, SortItem.VPLayer.HUD, SortItem.Translucency.NORMAL, font.MaterialID, 0, 0, 0);
            int nPrimitives = (verts.Count) / 3;
            RenderDelegate del = new RenderDelegate((effect, device, setMaterial) =>
            {
                //if (setMaterial)
                    device.SetTexture(0, bg.Texture);

                // Draw UI elements
                    device.DrawUserPrimitives<VertexPositionColorTex>(PrimitiveType.TriangleList, 2, arr);
                    device.SetTexture(0, font.Texture);
                device.DrawUserPrimitives<VertexPositionColorTex>(PrimitiveType.TriangleList, 6, nPrimitives-2, arr);
                //device.DrawPrimitives(PrimitiveType.TriangleList, 0, nPrimitives);
            });
            Renderer.Instance.drawCalls.Add(new KeyValuePair<ulong, RenderDelegate>(key, del));
        }
    }
}
