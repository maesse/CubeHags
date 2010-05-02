using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace CubeHags.client.gui
{
    public class LayoutSingle : LayoutManager
    {
        
        public LayoutSingle()
        {
            
        }


        public Size MinimumLayoutSize(Container parent)
        {
            throw new NotImplementedException();
        }

        public Size PreferredLayoutSize(Container parent)
        {
            Size preferred = new Size();
            foreach (Control control in parent.Controls)
            {
                Size dim = control.GetPreferredSize();
                preferred.Width += dim.Width;
                preferred.Height += dim.Height;
            }

            return preferred;
        }

        public void LayoutContainer(Container parent)
        {

            if (parent.ControlCount > 0)
            {
                Size dim = parent.GetControl(0).GetPreferredSize();
                parent.GetControl(0).Size = dim;
                parent.GetControl(0).Position = new Dimension(parent.Position.X  + (parent.Window.Size.Width / 2) - (dim.Width / 2), parent.Position.Y  + (parent.Window.Size.Height/2)  - (dim.Height / 2));
            }
        }



        #region LayoutManager Members


        public Size MaximumLayoutSize(Container parent)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
