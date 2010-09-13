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

        public override SlimDX.Result OnLostDevice()
        {
            foreach (Control ctrl in Controls)
                ctrl.OnLostDevice();
            return base.OnLostDevice();
        }

        public override SlimDX.Result OnResetDevice()
        {
            foreach (Control ctrl in Controls)
                ctrl.OnResetDevice();
            return base.OnResetDevice();
        }
    }
}
