using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CubeHags.client.gui
{
    class MainRibbon : RibbonMenu
    {
        RibbonMenuItem sourcemap;
        RibbonMenuItem renderer;


        public MainRibbon()
        {
            // Sourcemap
            sourcemap = new RibbonMenuItem("SourceMap", this);

            RibbonGroup group = new RibbonGroup("Loading", this);
            Button button = new Button("Load Sourcemap", this);
            button.Selected += new Button.ButtonSelectedEvent(LoadMapEvent);
            
            Button button2 = new Button("Unload Sourcemap", this);
            button2.Selected += new Button.ButtonSelectedEvent(UnLoadMapEvent);
            group.AddControl(button);
            group.AddControl(button2);
            sourcemap.AddItem(group);

            group = new RibbonGroup("Hags?",this);
            group.AddControl(new Button("Magic Button", this));
            sourcemap.AddItem(group);
            
            AddItem(sourcemap);

            // Renderer
            renderer = new RibbonMenuItem("Renderer", this);
            group = new RibbonGroup("Screenshot",this);
            button = new Button("Screenshot", this);
            group.AddControl(button);
            renderer.AddItem(group);
            AddItem(renderer);

            // Other
            AddItem(new RibbonMenuItem("Test2", this));
            AddItem(new RibbonMenuItem("Test3", this));
        }


        public void LoadMapEvent()
        {

            //string str = Renderer.Instance.gui.OpenFileDialog("BSP Files|*.bsp|All Files|*.*");
            //if (str != null)
            //{
            //    Renderer.Instance.LoadMap(str);
            //    //Renderer.Instance.LoadMap(@"client/data/map/albjergparken.bsp");
            //}

        }

        public void UnLoadMapEvent()
        {
            //Renderer.Instance.UnloadMap();
        }

    }
}
