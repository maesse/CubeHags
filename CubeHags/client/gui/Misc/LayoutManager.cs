using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace CubeHags.client.gui
{
    public interface LayoutManager
    {
        Size MinimumLayoutSize(Container parent);
        Size PreferredLayoutSize(Container parent);
        Size MaximumLayoutSize(Container parent);
        
        void LayoutContainer(Container parent);
        //void AddLayoutControl(string name, Control control);
        //void RemoveLayoutControl(Control control);
    }
}
