using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CubeHags.client.gui
{
    class WindowComparer : IComparer<Window>
    {
        public int Compare(Window x, Window y)
        {
            return y.Zvalue.CompareTo(x.Zvalue);

        }
        
    }

    class WindowComparerReversed : IComparer<Window>
    {
        public int Compare(Window x, Window y)
        {
            return x.Zvalue.CompareTo(y.Zvalue);

        }

    }
}
