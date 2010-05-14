using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.common;

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
            this.WindowSpawnPosition = Corner.MIDDLE;
            this.Resizeable = false;
            Init();
        }

        void Init()
        {
            GameName = new Label("Cube!", this);
            GameName.LabelFont = "biggerlabel";
            StartGameButton = new Button("New Game", this);
            StartGameButton.label.LabelFont = "biglabel";
            StartGameButton.Selected += new Button.ButtonSelectedEvent(StartGameHandler);
            SettingsButton = new Button("Settings", this);
            SettingsButton.label.LabelFont = "biglabel";
            SettingsButton.Selected += new Button.ButtonSelectedEvent(SettingsHandler);
            ExitButton = new Button("Exit", this);
            ExitButton.label.LabelFont = "biglabel";
            ExitButton.Selected += new Button.ButtonSelectedEvent(ExitHandler);

            panel.AddControl(GameName);
            panel.AddControl(StartGameButton);
            panel.AddControl(SettingsButton);
            panel.AddControl(ExitButton);
        }

        void StartGameHandler()
        {

        }

        void SettingsHandler()
        {
            // Hide this window, and show settings window
            this.Visible = false;
            WindowManager.Instance.settingsGUI.Visible = true;
        }

        void ExitHandler()
        {
            Common.Instance.Shutdown();
        }
    }
}
