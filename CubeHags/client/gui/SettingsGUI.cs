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
            VSyncButton.label.LabelFont = "biglabel";
            VSyncButton.Selected += new Button.ButtonSelectedEvent(VSyncHandler);
            vsyncContainer.AddControl(VSyncButton);
            this.panel.AddControl(vsyncContainer);

            // Fullscreen
            Panel fsPanel = new Panel(this);
            fsPanel.AddControl(new Label("Fullscreen:", this) { LabelFont = "biglabel" });
            FullscreenButton = new Button(CVars.Instance.Get("r_fs", "1", CVarFlags.ARCHIVE).Integer == 1 ? "Yes" : "No", this);
            FullscreenButton.label.LabelFont = "biglabel";
            FullscreenButton.Selected += new Button.ButtonSelectedEvent(FSHandler);
            fsPanel.AddControl(FullscreenButton);
            this.panel.AddControl(fsPanel);

            // Resolution
            Panel resPanel = new Panel(this);
            resPanel.AddControl(new Label("Resolution", this) { LabelFont = "biglabel" });
            ResolutionButton = new Button(CVars.Instance.Get("r_res", "1", CVarFlags.ARCHIVE).String, this);
            ResolutionButton.label.LabelFont = "biglabel";
            ResolutionButton.Selected += new Button.ButtonSelectedEvent(ResolutionHandler);
            resPanel.AddControl(ResolutionButton);
            this.panel.AddControl(resPanel);

            // Show FPS
            Panel fpsPanel = new Panel(this);
            fpsPanel.AddControl(new Label("Show FPS", this) { LabelFont = "biglabel" });
            ShowFPSButton = new Button(CVars.Instance.Get("r_showfps", "1", CVarFlags.ARCHIVE).Integer == 1 ? "Yes" : "No", this);
            ShowFPSButton.label.LabelFont = "biglabel";
            ShowFPSButton.Selected += new Button.ButtonSelectedEvent(ShowFPSHandler);
            fpsPanel.AddControl(ShowFPSButton);
            this.panel.AddControl(fpsPanel);
            
            this.panel.AddControl(BackButton);
        }

        void FSHandler()
        {
            // Toggle vsync
            if (CVars.Instance.Get("r_fs", "1", CVarFlags.ARCHIVE).Integer == 0)
            {
                CVars.Instance.Set("r_fs", "1");
                FullscreenButton.label.Text = "Yes";
            }
            else
            {
                CVars.Instance.Set("r_fs", "0");
                FullscreenButton.label.Text = "No";
            }
        }

        void ResolutionHandler()
        {
            // Toggle resolution
            string currentRes = CVars.Instance.Get("r_res", "1280x800", CVarFlags.ARCHIVE).String;
            // Find offset in valid resolutions
            int i;
            for (i = 0; i < Renderer.Instance.ValidResolutions.Count; i++)
            {
                string valRes = Renderer.Instance.ValidResolutions[i];
                if (currentRes.Equals(valRes))
                    break;
            }

            if (i >= Renderer.Instance.ValidResolutions.Count-1)
            {
                i = -1;
            }

            string nextRes = Renderer.Instance.ValidResolutions[i + 1];
            CVars.Instance.Set("r_res", nextRes);
            ResolutionButton.label.Text = nextRes;
        }

        // Toggle ShowFps
        void ShowFPSHandler()
        {
            if (CVars.Instance.Get("r_showfps", "1", CVarFlags.ARCHIVE).Integer == 0)
            {
                CVars.Instance.Set("r_showfps", "1");
                ShowFPSButton.label.Text = "On";
            }
            else
            {
                CVars.Instance.Set("r_showfps", "0");
                ShowFPSButton.label.Text = "Off";
            }
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
            // Check for new graphics settings
            if (CVars.Instance.FindVar("r_fs").Modified
                || CVars.Instance.FindVar("r_vsync").Modified
                || CVars.Instance.FindVar("r_res").Modified)
            {
                Renderer.Instance._sizeChanged = true;
            }

            // Switch back to MenuGUI
            this.Visible = false;
            WindowManager.Instance.menuGUI.Visible = true;
        }
    }
}
