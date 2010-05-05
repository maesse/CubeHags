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
        private Label clStats;
        private Label svStats;

        public ConnectGUI()
        {
            this.panel.Layout = new FlowLayout(false);
            this.Title = "Connect...";
            this.WindowSpawnPosition = Corner.MIDDLE;
            ConnectStatus = new Label("Connecting... ", this);
            this.panel.AddControl(ConnectStatus);
            infoLabel = new Label(".", this);
            panel.AddControl(infoLabel);
            cancelButton = new Button("Cancel", this);
            this.panel.AddControl(cancelButton);
            clStats = new Label("...", this);
            this.panel.AddControl(clStats);
            svStats = new Label("...", this);
            this.panel.AddControl(svStats);
        }

        public void DrawConnect()
        {
            connstate_t state = Client.Instance.cls.state;
            int retry = Client.Instance.clc.connectPacketCount;
            string servername = Client.Instance.cls.servername;
            string updateInfoString = Client.Instance.cls.updateInfoString;
            string messageString = Client.Instance.clc.serverMessage;
            //int cliNum = Client.Instance.cl.snap.ps.clientNum;

            ConnectStatus.Text = "Connecting to: " + servername;
            string s;
            switch (state)
            {
                case connstate_t.CONNECTING:
                    s = "Awaiting challenge..." + retry;
                    break;
                case connstate_t.CHALLENGING:
                    s = "Awaiting connection..." + retry;
                    break;
                case connstate_t.CONNECTED:
                    s = "Awaiting gamestate...";
                    break;
                case connstate_t.LOADING:
                    s = "Loading map...";
                    break;
                case connstate_t.PRIMED:
                    s = "Waiting for server...";
                    break;
                case connstate_t.ACTIVE:
                    s = "Connection complete :)";
                    break;
                default:
                    s = "N/A";
                    break;
            }

            infoLabel.Text = s;
            
            NetBaseStatistics stats = Net.Instance.ClientStatistic;
            if(stats != null) {
                clStats.Text = string.Format("Client: in: {0:0.00}kb/s {1:0}packet/s\n       out: {2:0.00}b/s {3:0}packet/s", stats.GetBytesReceivedPerSecond(NetTime.Now) / 1024f, stats.GetPacketsReceivedPerSecond(NetTime.Now), stats.GetBytesSentPerSecond(NetTime.Now) / 1024f, stats.GetPacketsSentPerSecond(NetTime.Now));

            }
        }
    }
}
