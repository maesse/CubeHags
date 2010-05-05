using System;
using System.Collections.Generic;
 
using System.Text;

namespace CubeHags.client.gui
{
    public class Panel : Container
    {
        public Panel(Window window) : base(window)
        {
            
        }

        public Panel(LayoutManager Layout,Window window) : base(window)
        {
            this.Layout = Layout;
        }

        public override System.Drawing.Size GetMaximumSize()
        {
            return MaxSize;
        }

        
    }
}
