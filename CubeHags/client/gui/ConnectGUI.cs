using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using Lidgren.Network;

namespace CubeHags.client.gui
{
    class ConnectGUI : Window
    {
        private Label ConnectStatus;
        private Label infoLabel;
        private Button cancelButton;
        private string loadString = "";

        public ConnectGUI()
        {
            this.panel.Layout = new FlowLayout(false);
            this.Title = "Connecting...";
            this.WindowSpawnPosition = Corner.MIDDLE;

            ConnectStatus = new Label("Connecting... ", this);
            this.panel.AddControl(ConnectStatus);

            infoLabel = new Label(".", this);
            panel.AddControl(infoLabel);

            cancelButton = new Button("Cancel", this);
            cancelButton.Selected += new Button.ButtonSelectedEvent(Cancel);
            this.panel.AddControl(cancelButton);
            Visible = false;
        }

        void Cancel()
        {
            Client.Instance.Disconnect(true);
            Visible = false;
        }

        public void LoadString(string str)
        {
            loadString = str;
        }

        public void DrawConnect()
        {
            ConnectState state = Client.Instance.state;
            int retry = Client.Instance.clc.connectPacketCount;
            string servername = Client.Instance.servername;
            string messageString = Client.Instance.clc.serverMessage;

            ConnectStatus.Text = "Connecting to: " + servername;
            string s;
            switch (state)
            {
                case ConnectState.CONNECTING:
                    s = "Awaiting challenge..." + (retry > 1 ? ""+retry : "");
                    Title = "Connecting..";
                    loadString = "";
                    break;
                case ConnectState.CHALLENGING:
                    s = "Awaiting connection...";
                    break;
                case ConnectState.CONNECTED:
                    s = "Awaiting gamestate...";
                    Title = "Connected...";
                    break;
                case ConnectState.LOADING:
                    s = "Loading " + loadString;
                    Title = "Loading...";
                    break;
                case ConnectState.PRIMED:
                    s = "Waiting for server...";
                    Title = "Waiting..";
                    break;
                case ConnectState.ACTIVE:
                    s = "Connection complete :)";
                    Visible = false;
                    break;
                default:
                    s = "N/A";
                    break;
            }

            infoLabel.Text = s;
        }
    }
}
