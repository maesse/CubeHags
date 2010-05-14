using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.client.common;

namespace CubeHags.client.gui
{
    public class SettingsGUI : Window
    {
        Button BackButton;
        Button VSyncButton;
        Button FullscreenButton;
        Button ResolutionButton;
        Button ShowFPSButton;

        

        public SettingsGUI()
        {
            this.Title = "Settings";
            this.panel.Layout = new FlowLayout(false);
            this.Resizeable = false;
            this.WindowSpawnPosition = Corner.MIDDLE;
            Init();
        }

        void Init()
        {
            BackButton = new Button("Back...", this);
            BackButton.label.LabelFont = "biglabel";
            BackButton.Selected += new Button.ButtonSelectedEvent(BackHandler);

            // Vsync
            Panel vsyncContainer = new Panel(this);
            vsyncContainer.AddControl(new Label("Toggle VSync:", this) { LabelFont = "biglabel" });
            VSyncButton = new Button(CVars.Instance.Get("r_vsync", "1", CVarFlags.ARCHIVE).Integer == 1? "On" : "Off", this);
            VSyncButton.label.LabelFont = "bigfont";
            VSyncButton.Selected += new Button.ButtonSelectedEvent(VSyncHandler);
            vsyncContainer.AddControl(VSyncButton);
            this.panel.AddControl(vsyncContainer);

            // Fullscreen
            Panel fsPanel = new Panel(this);
            fsPanel.AddControl(new Label("Fullscreen:", this) { LabelFont = "biglabel" });
            FullscreenButton = new Button(CVars.Instance.Get("r_fs", "1", CVarFlags.ARCHIVE).Integer == 1 ? "Yes" : "No", this);
            FullscreenButton.label.LabelFont = "bigfont";
            FullscreenButton.Selected += new Button.ButtonSelectedEvent(FSHandler);
            fsPanel.AddControl(FullscreenButton);
            this.panel.AddControl(fsPanel);

            // Resolution
            Panel resPanel = new Panel(this);
            resPanel.AddControl(new Label("Resolution", this) { LabelFont = "biglabel" });
            

            this.panel.AddControl(BackButton);
        }

        void FSHandler()
        {

        }

        void ResolutionHandler()
        {

        }

        void ShowFPSHandler()
        {

        }


        void VSyncHandler()
        {
            // Toggle vsync
            if (CVars.Instance.Get("r_vsync", "1", CVarFlags.ARCHIVE).Integer == 0)
            {
                CVars.Instance.Set("r_vsync", "1");
                VSyncButton.label.Text = "On";
            }
            else
            {
                CVars.Instance.Set("r_vsync", "0");
                VSyncButton.label.Text = "Off";
            }

            
        }

        void BackHandler()
        {
            // Todo: Save settings

            // Switch back to MenuGUI
            this.Visible = false;
            WindowManager.Instance.menuGUI.Visible = true;
        }
    }
}
