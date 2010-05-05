using System;
using System.Collections.Generic;
 
using System.Text;
using System.Drawing;
using SlimDX.Direct3D9;
using SlimDX;
using SlimDX.Design;

namespace CubeHags.client.gfx
{
    class TextureNode
    {
        public TextureNode[] Child = new TextureNode[2];
        public TextureNode Parent;
        public Rectangle Rectangle;
        public bool leaf = true;
        public bool Used = false;

        public TextureNode(Rectangle rect, TextureNode parent)
        {
            Parent = parent;
            this.Rectangle = rect;
        }

        public TextureNode Insert(ref Color3[] lightData, int width, int height, int offset, ref DataStream ds, bool flipY)
        {
            if (!leaf)
            {
                if (Child[0].Rectangle.Width >= width && Child[0].Rectangle.Height >= height)
                {
                    TextureNode newnode = Child[0].Insert(ref lightData, width, height, offset, ref ds, flipY);
                    if (newnode != null) return newnode;
                }

                if (Child[1].Rectangle.Width >= width && Child[1].Rectangle.Height >= height)
                    return Child[1].Insert(ref lightData, width, height, offset, ref ds, flipY);
                else
                    return null;
            }
            else
            {
                // allready used?
                if(Used)
                {
                    return null;
                }

                // not a fit?
                if(Rectangle.Width < width || Rectangle.Height < height)
                {
                    return null;
                }

                // perfect fit?
                if((Rectangle.Width == width && Rectangle.Height == height)) 
                {
                    
                    for (int j = 0; j < (width*height); j++)
                    {
                        int x = Rectangle.X + ((j) % (width));
                        int y = Rectangle.Y + ((j) / (width));
                        long pos = (y * 1024L * 8L) + (x * 8L);
                        if(ds.Position != pos)
                            ds.Seek(pos, System.IO.SeekOrigin.Begin);
                        Half[] half =  Half.ConvertToHalf(new float[] {lightData[offset + j].Red, lightData[offset + j].Green, lightData[offset + j] .Blue});
                        ds.Write<Half3>(new Half3(half[0], half[1], half[2]));
                    }
                    this.Used = true;
                    
                    return this;
                }

                // Else split up and make this a node
                int dw = Rectangle.Width - width;
                int dh = Rectangle.Height - height;

                if (dw > dh)
                {
                    Rectangle rect0 = new Rectangle(Rectangle.Left, Rectangle.Top, width , Rectangle.Height);
                    Rectangle rect1 = new Rectangle(Rectangle.Left + width, Rectangle.Top, Rectangle.Width-width, Rectangle.Height);
                    
                    Child[0] = new TextureNode(rect0, this);
                    Child[1] = new TextureNode(rect1, this);
                }
                else
                {
                    Rectangle rect0 = new Rectangle(Rectangle.Left, Rectangle.Top, Rectangle.Width, height);
                    Rectangle rect1 = new Rectangle(Rectangle.Left, Rectangle.Top+height, Rectangle.Width, Rectangle.Height-height);
                    Child[0] = new TextureNode(rect0, this);
                    Child[1] = new TextureNode(rect1, this);
                }
                leaf = false;
                Used = true;
                return Child[0].Insert(ref lightData, width, height, offset, ref ds, flipY);
            }
        }

        // Writes this node to console
        public void WriteNode(int indent)
        {
            string indentStr = String.Empty;
            for (int i = 0; i < indent; i++)
            {
                indentStr += " ";
            }

            if (leaf && Used)
                indentStr += "*";
            else
                indentStr += "-";

            System.Console.WriteLine(string.Format("{0}:{1} Rect: {2}", indentStr, (leaf? "Leaf":"Node"), Rectangle));

            if (!leaf)
            {
                indent++;
                Child[0].WriteNode(indent);
                Child[1].WriteNode(indent);
            }
        }

        // Writes whole tree to console provided with a random node from the tree
        public static void WriteTree(TextureNode SomeNode)
        {
            TextureNode master = SomeNode;

            // Get master node
            while (master.Parent != null)
            {
                master = master.Parent;
            }

            master.WriteNode(0);
        }
    }
}
