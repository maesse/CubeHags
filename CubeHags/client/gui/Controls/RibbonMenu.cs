using System;
using System.Collections.Generic;
 
using System.Text;
using SlimDX.Direct3D9;
using System.Drawing;

namespace CubeHags.client.gui
{
    class RibbonMenu : Window
    {
        
        // Show toggling
        public bool ShowRibbon { get { return _ShowRibbon; } set { if (value != _ShowRibbon) { _ShowRibbon = value; UpdateSize(); } } }
        private bool _ShowRibbon = false;
        private Button ToggleRibbonButton;

        // MenuItems
        private List<RibbonMenuItem> _Items = new List<RibbonMenuItem>();
        public List<RibbonMenuItem> Items { get { return _Items; } }
        public RibbonMenuItem ActiveItem = null;

        // Textures
        private Texture backgroundTex = null;
        private Rectangle backgroundRect;
        public Texture buttonTex = null;
        public Rectangle[] buttonRect;
        public Texture groupTex = null;
        public Rectangle[] groupRect;
        public Texture gradientTex = null;
        public Rectangle gradientRect;
        private Rectangle gradientRenderRect = new Rectangle(0,0,Renderer.Instance.RenderSize.Width, 10);

        public Sprite sprite;
        private Rectangle bgRect;
        private SizeF bgSize = new SizeF();
        private PointF pos = new PointF();

        public RibbonMenu()
        {
            // Set up size and position
            this.Size = this.PreferredSize = new System.Drawing.Size(Renderer.Instance.RenderSize.Width, 100);
            UpdateSize();
            this.WindowSpawnPosition = Corner.TOPLEFT;
            FlowLayout layout = new FlowLayout(true);
            //layout.centered = false;
            layout.Margin = 6;
            panel.Layout = layout;

            this.ForcePanelSize = true;
            this.PanelSize = new Size(Renderer.Instance.RenderSize.Width, 23);

            // Make window borderless
            this.Borderless = true;
            this.Resizeable = false;

            // Load textures..
            //backgroundTex = TextureManager.Instance.LoadTexture("gui/ribbon/ribbonbg.png");
            backgroundRect = new Rectangle(0, 0, 19, 99);
            //buttonTex = TextureManager.Instance.LoadTexture("gui/ribbon/ribbonbutton.png");
            buttonRect = new Rectangle[3];
            buttonRect[0] = new Rectangle(0,0,5,24); // left
            buttonRect[1] = new Rectangle(5,0,24,24); // middle (strect-able)
            buttonRect[2] = new Rectangle(28,0,5,24); // right
            //groupTex = TextureManager.Instance.LoadTexture("gui/ribbon/ribbongroup.png");
            groupRect = new Rectangle[9];
            groupRect[0] = new Rectangle(0,0,6,8);
            groupRect[1] = new Rectangle(6,0,44,8);
            groupRect[2] = new Rectangle(50,0,6,8);
            groupRect[3] = new Rectangle(0, 20, 6, 8);
            groupRect[4] = new Rectangle(6, 20, 44, 8);
            groupRect[5] = new Rectangle(50, 20, 6, 8);
            groupRect[6] = new Rectangle(0, 63, 6, 8);
            groupRect[7] = new Rectangle(6, 63, 44, 8);
            groupRect[8] = new Rectangle(50, 63, 6, 8);
            //gradientTex = TextureManager.Instance.LoadTexture("gui/ribbon/blacktransgrad.png");
            gradientRect = new Rectangle(0, 5, 16, 27);
            
            sprite = new Sprite(Renderer.Instance.device);

            // Set up button
            ToggleRibbonButton = new Button("+", this);
            ToggleRibbonButton.Selected += new Button.ButtonSelectedEvent(ToggleShowRibbon);
            panel.AddControl(ToggleRibbonButton);

            
        }

        public void AddItem(RibbonMenuItem item)
        {
            _Items.Add(item);
            panel.AddControl(item);
        }

        public void ShowItem(RibbonMenuItem item)
        {
            if (ActiveItem != null)
                ActiveItem.Showing = false;
            ActiveItem = item;
            ActiveItem.Showing = true;

            if (!ShowRibbon)
                ToggleShowRibbon();
        }

        public override void Render()
        {
            bgRect = backgroundRect;
            bgSize.Width = Size.Width;
            bgSize.Height = Size.Height;
            pos.X = Position.X;
            pos.Y = Position.Y;
            
            if (!ShowRibbon)
            {
                bgRect.Height = 23;
                bgSize.Height = 23;
            }
            gradientRenderRect.Y = (int)bgSize.Height;
            //sprite.Begin(SpriteFlags.AlphaBlend);
            //sprite.Draw2D(backgroundTex, bgRect, bgSize, pos, Color.White);
            //sprite.Draw2D(gradientTex, gradientRect, gradientRenderRect.Size, gradientRenderRect.Location, Color.White);

            //sprite.End();

            base.Render();
        }

        public void ToggleShowRibbon()
        {
            ShowRibbon = !ShowRibbon;
            ToggleRibbonButton.label.Text = (ShowRibbon ? "-" : "+");
            if (!ShowRibbon && ActiveItem != null)
                ActiveItem.Showing = false;
            else if (ShowRibbon && ActiveItem != null)
                ActiveItem.Showing = true;
            else if (ShowRibbon && ActiveItem == null)
            {
                if (_Items.Count > 0)
                {
                    ActiveItem = _Items[0];
                    ActiveItem.Showing = true;
                }
            }
        }

        // Updates size based on state of ShowRibbon
        public void UpdateSize()
        {
            if (ShowRibbon)
                Resize(new Size(Renderer.Instance.RenderSize.Width, 100));
            else
                Resize(new Size(Renderer.Instance.RenderSize.Width, 23));
        }

    }
}
