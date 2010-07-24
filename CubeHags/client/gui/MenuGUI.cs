using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.common;
using CubeHags.client.common;

namespace CubeHags.client.gui
{
    public class MenuGUI : Window
    {
        Label GameName;
        Button StartGameButton;
        Button JoinGameButton;
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
            StartGameButton = new Button("Create Game", this);
            StartGameButton.label.LabelFont = "biglabel";
            StartGameButton.Selected += new Button.ButtonSelectedEvent(StartGameHandler);
            JoinGameButton = new Button("Join Game", this);
            JoinGameButton.label.LabelFont = "biglabel";
            JoinGameButton.Selected += new Button.ButtonSelectedEvent(JoinGameHandler);
            SettingsButton = new Button("Settings", this);
            SettingsButton.label.LabelFont = "biglabel";
            SettingsButton.Selected += new Button.ButtonSelectedEvent(SettingsHandler);
            ExitButton = new Button("Exit", this);
            ExitButton.label.LabelFont = "biglabel";
            ExitButton.Selected += new Button.ButtonSelectedEvent(ExitHandler);

            panel.AddControl(GameName);
            panel.AddControl(StartGameButton);
            panel.AddControl(JoinGameButton);
            panel.AddControl(SettingsButton);
            panel.AddControl(ExitButton);
        }

        void JoinGameHandler()
        {
            if (WindowManager.Instance.serverList == null)
            {
                WindowManager.Instance.serverList = new ServerListUI();
                WindowManager.Instance.AddWindow(WindowManager.Instance.serverList);
            }
            else
            {
                WindowManager.Instance.serverList.Visible = true;
                WindowManager.Instance.MoveToFront(WindowManager.Instance.serverList);
            }
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "localservers");
        }

        void StartGameHandler()
        {
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "map de_dust2");
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
