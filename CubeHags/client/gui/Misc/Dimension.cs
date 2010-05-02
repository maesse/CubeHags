using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CubeHags.client.gui
{
    public class Dimension
    {
        public int X, Y;

        public Dimension(int X, int Y)
        {
            this.X = X;
            this.Y = Y;
        }

        public Dimension()
        {
            X = 0;
            Y = 0;
        }

        public static Dimension operator +(Dimension dim1, Dimension dim2)
        {
            Dimension res = new Dimension();
            res.X = dim1.X + dim2.X;
            res.Y = dim1.Y + dim2.Y;
            return res;
        }



        public bool Equals(Dimension dimension)
        {
            if (this.X == dimension.X && this.Y == dimension.Y)
                return true;

            return false;
        }

        
    }
}
