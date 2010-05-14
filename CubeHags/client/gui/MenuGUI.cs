using System;
using System.Collections.Generic;
using System.Text;

namespace CubeHags.client.gui
{
    public class MenuGUI : Window
    {
        Label GameName;
        Button StartGameButton;
        Button SettingsButton;
        Button ExitButton;

        public MenuGUI()
        {
            this.Title = "Main Menu";
            this.panel.Layout = new FlowLayout(false);
        }

        void Init()
        {
            GameName = new Label("Cube!", this);
            GameName.LabelFont = "biglabel";
            panel.AddControl(GameName);
            StartGameButton = new Button("New Game", this);
            panel.AddControl(StartGameButton);
            ExitButton = new Button("Exit", this);
            panel.AddControl(ExitButton);
        }
    }
}
