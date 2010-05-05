using System;
using System.Collections.Generic;
 
using System.Text;
using System.Drawing;

namespace CubeHags.client.gui
{
    public class FlowLayout : LayoutManager
    {
        private bool horizontal = true;
        public int Margin = 2;
        public bool centered = true;

        public FlowLayout(bool horizontal)
        {
            this.horizontal = horizontal;
        }

        public Size MinimumLayoutSize(Container parent)
        {
            Size result = new Size();

            foreach (Control control in parent.Controls)
            {
                Size dim = control.GetMinimumSize();
                if (horizontal)
                {

                    result.Width += dim.Width + Margin;
                    if (dim.Height > result.Height)
                        result.Height = dim.Height;
                }
                else
                {
                    result.Height += dim.Height + Margin;
                    if (dim.Width > result.Width)
                        result.Width = dim.Width;
                }
            }
            if (horizontal)
                result.Width -= Margin;
            else
                result.Width -= Margin;

            return result;
        }

        public Size PreferredLayoutSize(Container parent)
        {
            Size result = new Size();
            
            foreach (Control control in parent.Controls)
            {
                Size dim = control.GetPreferredSize();
                if (horizontal)
                {

                    result.Width += dim.Width + Margin;
                    if (dim.Height > result.Height)
                        result.Height = dim.Height;
                }
                else
                {
                    result.Height += dim.Height + Margin;
                    if (dim.Width > result.Width)
                        result.Width = dim.Width;
                }
            }
            if (horizontal)
                result.Width -= Margin;
            else
                result.Width -= Margin;

            if (result.Width < parent.PreferredSize.Width)
                result.Width = parent.PreferredSize.Width;

            if (result.Height < parent.PreferredSize.Height)
                result.Height = parent.PreferredSize.Height;

            return result;
        }

        public void LayoutContainer(Container parent)
        {
            if (parent.ControlCount > 0)
            {
                Size offset = new Size();
                Size preferredTotal = PreferredLayoutSize(parent);
                Size minTotal = MinimumLayoutSize(parent);
                Size maxTotal = MaximumLayoutSize(parent);
                Size parentSize = parent.Size - new Size(parent.Margin*2, parent.Margin*2);
                if (parentSize.Width < 0 || parentSize.Height < 0)
                    parentSize = new Size();
                Dimension parentPosition = new Dimension(parent.Position.X + parent.Margin, parent.Position.Y + parent.Margin);
                int sizeCase = 0;

                // resize parent?
                if (parentSize.Height == 0 || parentSize.Width == 0)
                {
                    parentSize = preferredTotal;
                    parent.Size = preferredTotal + new Size(parent.Margin*2, parent.Margin*2);
                    //sizeCase = -1;
                }
                // parent too small for minimum?
                if (parentSize.Height < minTotal.Height || parentSize.Width < minTotal.Width)
                {
                    // clip
                    sizeCase = 0;
                    if (parent.AutoResize)
                    {
                        if(parentSize.Height < preferredTotal.Height)
                            parentSize.Height = preferredTotal.Height;
                        if (parentSize.Width < preferredTotal.Width)
                            parentSize.Width = preferredTotal.Width;
                        sizeCase = 2;
                    }
                }
                // Parent too small to preffered?
                else if (parentSize.Width < preferredTotal.Width || parentSize.Height < preferredTotal.Height)
                {
                    // Use minimum
                    //minimum = true;
                    //preferredTotal = MinimumLayoutSize(parent);
                    sizeCase = 1;
                }
                // Parent too small to use maximum?
                else if (parentSize.Width < maxTotal.Width || parentSize.Height < maxTotal.Height)
                {
                    // Use preferred
                    //
                    sizeCase = 2;
                }
                // parent fits maximum?
                else
                {
                    // Use maximum
                    sizeCase = 3;
                }
                
                

                foreach (Control control in parent.Controls)
                {
                    Size controlSize = new Size();
                    switch (sizeCase)
                    {
                        case 0:
                            // Use Clipping
                            break;
                        case 1:
                            // Use minimum
                            controlSize = control.GetMinimumSize();
                            break;
                        case 2:
                            // Use preferred
                            controlSize = control.GetPreferredSize();
                            //if (horizontal)
                            //{
                            //    controlSize = new Size(pref.Width, parentSize.Height);
                            //}
                            //else
                            //{
                            //    controlSize = new Size(parentSize.Width, pref.Height);
                            //}
                            break;
                        case 3:
                            // Use maximum
                            controlSize = control.GetMaximumSize();
                            break;
                    }

                    //Size pref;
                    //if (!minimum)
                    //    pref = control.GetPreferredSize();
                    //else
                    //    pref = control.GetMinimumSize();


                    control.Size = controlSize;
                    if (!centered)
                    {
                        control.Position = new Dimension(parentPosition.X + offset.Width, parentPosition.Y + offset.Height);
                    }
                    else
                    {
                        int centerxoffset = (horizontal ? 0 : (parentSize.Width - controlSize.Width) / 2);
                        int centeryoffset = (!horizontal ? 0 : (parentSize.Height - controlSize.Height) / 2);
                        control.Position = new Dimension(parentPosition.X + offset.Width + centerxoffset, parentPosition.Y + offset.Height + centeryoffset);
                    }
                    control.Bound = new System.Drawing.Rectangle(control.Position.X, control.Position.Y, controlSize.Width, controlSize.Height);

                    
                    if (horizontal)
                    {
                        offset.Width += controlSize.Width + Margin;
                    }
                    else
                    {
                        offset.Height += controlSize.Height + Margin;
                    }
                }
                foreach (Control control in parent.Controls)
                {
                    if (control is Container)
                        ((Container)control).DoLayout();
                }
            }
        }



        #region LayoutManager Members


        public Size MaximumLayoutSize(Container parent)
        {
            Size max = new Size();
            foreach (Control control in parent.Controls)
            {
                Size size = control.GetMaximumSize();
                if (horizontal)
                {
                    //max controls height
                    if (size.Height > max.Height)
                        max.Height = size.Height;
                    //sum controls width
                    max.Width += size.Width;
                }
                else
                {
                    //max controls width
                    if (size.Width > max.Width)
                        max.Width = size.Width;
                    //sum controls height
                    max.Height += size.Height;
                }
            }
            return max;
        }

        #endregion
    }
}
