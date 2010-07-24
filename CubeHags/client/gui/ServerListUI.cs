using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.client.common;

namespace CubeHags.client.gui
{
    public class ServerListUI : Window
    {
        List<ServerListItemUI> servers = new List<ServerListItemUI>();
        Button closeButton;
        public ServerListUI()
        {
            // Setup window
            Title = "LAN Server List";
            panel.Layout = new FlowLayout(false);
            WindowSpawnPosition = Corner.MIDDLE;
            Size = new System.Drawing.Size(400, 300);
            Bound.Size = Size;

            closeButton = new Button("Close", this);
            closeButton.Selected += new Button.ButtonSelectedEvent(HideWindow);
        }

        void HideWindow()
        {
            this.Visible = false;
        }

        public override void Update()
        {
            base.Update();

            if (!Client.Instance.HasNewServers)
                return;

            Client.Instance.HasNewServers = false;

            // we have new servers
            servers.Clear();
            panel.Controls.Clear();
            serverInfo_t info;
            for (int i = 0; i < Client.Instance.localServers.Count; i++)
            {
                info = Client.Instance.localServers[i];
                ServerListItemUI item = new ServerListItemUI(this, info);
                panel.AddControl(item);
            }
            panel.AddControl(closeButton);
        }
    }

    public class ServerListItemUI : Panel
    {
        serverInfo_t info;
        Label serverIP;
        Button joinButton;

        public ServerListItemUI(Window window, serverInfo_t info) : base(window)
        {
            this.Layout = new FlowLayout(true);
            this.info = info;
            this.joinButton = new Button("Join", window);
            joinButton.Selected += new Button.ButtonSelectedEvent(JoinServer);
            serverIP = new Label("Address: " + info.adr.ToString(), window);
            this.AddControl(serverIP);
            this.AddControl(joinButton);
        }

        void JoinServer()
        {
            string connectString = info.adr.ToString();
            Commands.Instance.ExecuteText(Commands.EXECTYPE.EXEC_NOW, "connect " + connectString);
            WindowManager.Instance.serverList.Visible = false;
        }
    }
}
