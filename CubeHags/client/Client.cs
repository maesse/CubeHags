using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using SlimDX.Windows;
using CubeHags.client.input;
using CubeHags.client.common;
using System.Net;
using SlimDX;
using Lidgren.Network;
using CubeHags.client.render;
using CubeHags.client.gfx;
using CubeHags.server;
using CubeHags.client.gui;
using CubeHags.client.cgame;

namespace CubeHags.client
{
    public sealed partial class Client
    {
        private static readonly Client _Instance = new Client();
        public static Client Instance { get { return _Instance; } }
        public RenderForm form;

        public CVar cl_timeout = CVars.Instance.Get("cl_timeout", "30", CVarFlags.NONE);
        public CVar cl_maxpackets = CVars.Instance.Get("cl_cmdrate", "116", CVarFlags.ARCHIVE);
        public CVar cl_packetdup = CVars.Instance.Get("cl_cmdbackup", "1", CVarFlags.ARCHIVE);
        public CVar cl_timeNudge = CVars.Instance.Get("cl_timeNudge", "0", CVarFlags.TEMP);
        public CVar sensitivity = CVars.Instance.Get("sensitivity", "1", CVarFlags.ARCHIVE);
        public CVar cl_nodelta = CVars.Instance.Get("cl_nodelta", "0", CVarFlags.NONE);

        public ConnectState state;				// connection status
        public string servername;		// name of server from original connect (used by reconnect)

        public int frametime;			// msec since last frame
        public int realtime;			// 

        public clientActive cl = new clientActive();
        public clientConnect clc = new clientConnect();

        public Cinematic cin;

        public Lagometer lagometer = new Lagometer();

        public List<serverInfo_t> localServers = new List<serverInfo_t>();
        public bool HasNewServers = false;

        Ping[] PingList = new Ping[32];

        public Client()
        {

        }

        /*
       =================
       CL_PacketEvent

       A packet has arrived from the main event loop
       =================
       */
        public void PacketEvent(Net.Packet packet)
        {
             
            clc.lastPacketTime = realtime;

            if (packet.Type == NetMessageType.OutOfBandData)
            {
                ConnectionlessPacket(packet);
                return;
            }

            if (state < ConnectState.CONNECTED)
                return; // can't be a valid sequenced packet

            //
            // packet from server
            //
            if (!IPAddress.Equals(packet.Address.Address, clc.netchan.remoteAddress.Address))
            {
                Common.Instance.WriteLine("{0}: sequence packet without connection", packet.Address.Address.ToString());
                return;
            }

            if (!NetChan_Process(clc.netchan, packet))
                return;     // out of order, duplicated, etc

            // the header is different lengths for reliable and unreliable messages
            int headerBytes = packet.Buffer.Position;

            // track the last message received so it can be returned in 
            // client messages, allowing the server to detect a dropped
            // gamestate
            int oldpos = packet.Buffer.Position;
            packet.Buffer.Position = 0;
            clc.serverMessageSequence = packet.Buffer.ReadInt32();
            packet.Buffer.Position = oldpos;
            clc.lastPacketTime = realtime;
            ParseServerMessage(packet);
        }


        /*
        ===================
        CL_ForwardCommandToServer

        adds the current command line as a clientCommand
        things like godmode, noclip, etc, are commands directed to the server,
        so when they are typed in at the console, they will need to be forwarded.
        ===================
        */
        public void CL_ForwardCommandToServer(string text, string[] tokens)
        {
            string cmd = tokens[0];

            // ignore key up
            if (cmd[0] == '-')
                return;

            if ((int)state < (int)ConnectState.CONNECTED || cmd[0] == '+')
            {
                Common.Instance.WriteLine("Unknown command \"{0}^7\"\n", cmd);
                return;
            }

            if (tokens.Length > 1)
                AddReliableCommand(text, false);
            else
                AddReliableCommand(cmd, false);
        }

        void ConnectionlessPacket(Net.Packet packet)
        {
            string s = packet.Buffer.ReadString();
            string[] tokens = Commands.TokenizeString(s);

            Common.Instance.WriteLine("CL Packet: {0}:{1}: {2}", packet.Address.Address, packet.Address.Port, s);
            string c = tokens[0];
            if (c.Equals("challengeResponse"))
            {
                if (state != ConnectState.CONNECTING)
                {
                    Common.Instance.WriteLine("Unwanted challenge response recieved. Ignored.");
                    return;
                }

                if (!IPAddress.Equals(clc.serverAddress.Address, packet.Address.Address))
                {
                    // This challenge response is not coming from the expected address.
                    // Check whether we have a matching client challenge to prevent
                    // connection hi-jacking.

                    c = tokens[2];
                    if (!int.Parse(c).Equals(clc.challenge))
                    {
                        Common.Instance.WriteLine("Challenge response recieved from unexpected source. Ignored.");
                        return;
                    }
                }

                // start sending challenge response instead of challenge request packets
                clc.challenge = int.Parse(tokens[1]);
                state = ConnectState.CHALLENGING;
                clc.connectPacketCount = 0;
                clc.connectTime = -99999;

                // take this address as the new server address.  This allows
                // a server proxy to hand off connections to multiple servers
                clc.serverAddress = packet.Address;
                Common.Instance.WriteLine("Challenge response: {0}", clc.challenge);
                return;
            }

            // server connection
            if (c.Equals("connectResponse"))
            {
                if ((int)state >= (int)ConnectState.CONNECTED)
                {
                    Common.Instance.WriteLine("Duplicate connect recieved. Ignored");
                    return;
                }

                if (state != ConnectState.CHALLENGING)
                {
                    Common.Instance.WriteLine("connectResponse packet while not connecting. Ignored.");
                    return;
                }

                if (!IPAddress.Equals(packet.Address.Address, clc.serverAddress.Address))
                {
                    Common.Instance.WriteLine("connectResponse from wrong address. Ignored");
                }

                clc.netchan = Net.Instance.NetChan_Setup(Net.NetSource.CLIENT, packet.Address, CVars.Instance.VariableIntegerValue("net_qport"));
                Net.Instance.ClientConnect(packet.Address);
                state = ConnectState.CONNECTED;
                clc.lastPacketSentTime = -99999;   // send first packet immediately
                return;
            }

            if (c.Equals("print"))
            {
                s = tokens[1];
                clc.serverMessage = s;
                Common.Instance.WriteLine(s);
                return;
            }

            Common.Instance.WriteLine("Unknown connectionless packet: {0}" + s);
        }

        /*
        =================
        CL_FlushMemory

        Called by CL_MapLoading, CL_Connect_f, CL_PlayDemo_f, and CL_ParseGamestate the only
        ways a client gets into a game
        Also called by Com_Error
        =================
        */
        void CL_FlushMemory()
        {
            // Shutdown all client stuff
            ShutdownAll();

            if (!Common.Instance.sv_running.Bool)
            {
                Game.Instance.ShutdownGame(0);
                ClipMap.Instance.ClearMap();
            }
        }

        internal void Frame(float msec)
        {
            if (Common.Instance.cl_running.Integer == 0)
                return;

            // decide the simulation time
            frametime = (int)msec;
            realtime += frametime;

            // see if we need to update any userinfo
            CheckUserInfo();

            // if we haven't gotten a packet in a long time,
            // drop the connection
            CheckTimeout();

            // send intentions now
            //Renderer.Instance.form.
            Input.Instance.Update();
            Input.Instance.SendCmd();

            // resend a connection request if necessary
            CheckForResend();

            // decide on the serverTime to render
            SetCGameTime();

            // update the screen
            UpdateScreen();
            EndFrame();

            cin.RunCinematic();
        }

        public void Init()
        {
            Common.Instance.WriteLine("------- Client initialization --------");

            form = new RenderForm("CubeHags")
            {
                ClientSize = Renderer.Instance.RenderSize,
                StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
            };

            for (int i = 0; i < PingList.Length; i++)
            {
                PingList[i] = new Ping();
            }

            ClearState();
            state = ConnectState.DISCONNECTED;
            realtime = 0;

            CVars.Instance.Get("name", "UnknownCube", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("rate", "25000", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("model", "unknown", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("cl_updaterate", "40", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("cl_showTimeDelta", "0", CVarFlags.TEMP);
            CVars.Instance.Get("cl_timeout", "30", CVarFlags.NONE);

            cin = new Cinematic();

            // Register commands
            Commands.Instance.AddCommand("cinematic", new CommandDelegate(cin.PlayCinematic_f));
            Commands.Instance.AddCommand("connect", new CommandDelegate(CL_Connect_f));
            Commands.Instance.AddCommand("localservers", new CommandDelegate(CL_LocalServers_f));
            Commands.Instance.AddCommand("cmd", new CommandDelegate(CL_ForwardToServer_f));
            Commands.Instance.AddCommand("configstrings", new CommandDelegate(CL_Configstrings_f));
            Commands.Instance.AddCommand("clientinfo", new CommandDelegate(CL_Clientinfo_f));
            Commands.Instance.AddCommand("disconnect", new CommandDelegate(CL_Disconnect_f));
            //Commands.Instance.AddCommand("rcon", new CommandDelegate(CL_Rcon_f));
            Commands.Instance.AddCommand("ping", new CommandDelegate(CL_Ping_f));
            Commands.Instance.AddCommand("serverstatus", new CommandDelegate(CL_ServerStatus_f));
            Commands.Instance.AddCommand("model", new CommandDelegate(CL_SetModel_f));

            Renderer.Instance.Init(form);
            Input.Instance.InitializeInput();
            HagsConsole.Instance.Init();

            CVars.Instance.Set("cl_running", "1");
            Common.Instance.WriteLine("------- Client initialization Complete --------");
        }

        void CL_SetModel_f(string[] tokens)
        {
            if (tokens.Length == 1)
            {
                Common.Instance.WriteLine("model is set to {0}", CVars.Instance.VariableString("model"));
                return;
            }

            CVars.Instance.Set("model", tokens[1]);
            //CVars.Instance.Set("headmodel", tokens[1]);
        }

        void CL_ServerStatus_f(string[] tokens)
        {
            if (tokens.Length > 1 || state != ConnectState.ACTIVE)
            {
                Common.Instance.WriteLine("CL_ServerStatus_f: Status for non-connected server not implemented");
                return;
            }

            Net.Instance.OutOfBandMessage(Net.NetSource.CLIENT, clc.serverAddress, "getstatus");

            // FIX
        }

        void CL_Ping_f(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                Common.Instance.WriteLine("usage: ping server");
                return;
            }

            string server = tokens[1];
            IPEndPoint endp = Net.StringToAddr(server);
            if (endp == null)
                return;

            Ping ping = FindFreePing();
            ping.Addr = endp;
            ping.Start = (int)Common.Instance.Milliseconds();
            ping.Time = 0;


            Net.Instance.OutOfBandMessage(Net.NetSource.CLIENT, ping.Addr, "getinfo xxx");
        }

        public Ping FindFreePing()
        {
            Ping ping;
            for (int i = 0; i < 32; i++)
            {
                ping = PingList[i];

                // Find free ping slot
                if (ping.Addr != null)
                {
                    if (ping.Time == 0)
                    {
                        if (Common.Instance.Milliseconds() - ping.Start < 500)
                            continue; // Still waiting for answer
                    }
                    else if (ping.Time < 500)
                    {
                        // results have not been queried
                        continue;
                    }
                }
                
                // clear it
                ping.Addr = null;
                return ping;
            }

            // Use oldest entry
            int oldest = int.MinValue;
            Ping best = PingList[0];
            Ping current;
            for (int i = 0; i < PingList.Length; i++)
            {
                current = PingList[i];
                int time = (int)(Common.Instance.Milliseconds() - current.Start);
                if (time > oldest)
                {
                    best = current;
                    oldest = time;
                }
            }
            return best;
        }

        void CL_Disconnect_f(string[] tokens)
        {
            if (state != ConnectState.DISCONNECTED && state != ConnectState.CINEMATIC)
            {
                Common.Instance.Error("FIX DISCONNECT");
            }
        }

        void CL_Clientinfo_f(string[] tokens)
        {
            Common.Instance.WriteLine("---- Client Information ----");
            Common.Instance.WriteLine("State: {0}", Enum.GetName(typeof(ConnectState), state));
            Common.Instance.WriteLine("Server: {0}", (servername==null||servername=="") ? "Not connected" : servername);
            Common.Instance.WriteLine("User info settings:");
            Info.Print(CVars.Instance.InfoString(CVarFlags.USER_INFO));
            Common.Instance.WriteLine("----------------------------");
        }

        // Write out Configstrings to the console
        void CL_Configstrings_f(string[] tokens)
        {
            if (state != ConnectState.ACTIVE)
            {
                Common.Instance.WriteLine("Not connected to a server.");
                return;
            }

            foreach(int key in cl.gamestate.data.Keys)
            {
                Common.Instance.WriteLine("{0:0000}: {1}", key, cl.gamestate.data[key]);
            }
        }

        // Forward a command to the server
        void CL_ForwardToServer_f(string[] tokens) 
        {
            if (state != ConnectState.ACTIVE)
            {
                Common.Instance.WriteLine("Not connected to a server.");
                return;
            }

            // don't forward the first argument
            if (tokens.Length > 1)
            {
                AddReliableCommand(Commands.Args(tokens), false);
            }
        }

        void CheckTimeout()
        {
            if (state >= ConnectState.CONNECTED && state != ConnectState.CINEMATIC && realtime - clc.lastPacketTime > cl_timeout.Value * 1000)
            {
                if (++cl.timeoutCount > 5)
                {
                    Common.Instance.WriteLine("\nServer connection timed out.");
                    Disconnect(true);
                    return;
                }
            }
            else
                cl.timeoutCount = 0;
        }


        /*
        =====================
        CL_ClearState

        Called before parsing a gamestate
        =====================
        */
        void ClearState()
        {
            // Clean out the client state.
            cl = new clientActive();
        }

        /*
        =====================
        CL_Disconnect

        Called when a connection, demo, or cinematic is being terminated.
        Goes from a connected state to either a menu state or a console state
        Sends a disconnect message to the server
        This is also called on Com_Error and Com_Quit, so it shouldn't cause any errors
        =====================
        */
        public void Disconnect(bool showMainMenu)
        {
            WindowManager.Instance.connectGUI.Visible = false;
            if (Common.Instance.cl_running.Integer == 0)
            {
                return;
            }

            // send a disconnect message to the server
            // send it a few times in case one is dropped
            if ((int)state >= (int)ConnectState.CONNECTED)
            {
                AddReliableCommand("disconnect", true);
                Input.Instance.WritePacket();
                Input.Instance.WritePacket();
                Input.Instance.WritePacket();
            }

            ClearState();

            // wipe the client connection
            clc = new clientConnect();
            state = ConnectState.DISCONNECTED;
        }

        void CheckUserInfo()
        {
            // don't add reliable commands when not yet connected
            if ((int)state < (int)ConnectState.CHALLENGING)
                return;

            // send a reliable userinfo update if needed
            if ((CVars.Instance.modifiedFlags & CVarFlags.USER_INFO) == CVarFlags.USER_INFO)
            {
                CVars.Instance.modifiedFlags &= ~CVarFlags.USER_INFO;
                AddReliableCommand(string.Format("userinfo \"{0}\"", CVars.Instance.InfoString(CVarFlags.USER_INFO)), false);
            }
        }


        /*
        ======================
        CL_AddReliableCommand

        The given command will be transmitted to the server, and is gauranteed to
        not have future usercmd_t executed before it is executed
        ======================
        */
        public void AddReliableCommand(string cmd, bool isDisconnectCmd)
        {
            int unacknowledged = clc.reliableSequence - clc.reliableAcknowledge;
            // if we would be losing an old command that hasn't been acknowledged,
            // we must drop the connection
            // also leave one slot open for the disconnect command in this case.
            if ((isDisconnectCmd && unacknowledged > 64) ||
                (!isDisconnectCmd && unacknowledged >= 64))
            {
                Common.Instance.Error("Client command overflow");
            }

            clc.reliableCommands[++clc.reliableSequence & 63] = cmd;
        }

        // Prints text to console
        public void ConsolePrint(string str)
        {
            HagsConsole.Instance.AddLine(str);
        }

        /*
        =====================
        CL_MapLoading

        A local server is starting to load a map, so update the
        screen to let the user know about it, then dump all client
        memory on the hunk from cgame, ui, and renderer
        =====================
        */
        internal void MapLoading()
        {
            if (!Common.Instance.cl_running.Bool)
                return;

            // if we are already connected to the local host, stay connected
            if ((int)state >= (int)ConnectState.CONNECTED && servername.Equals("localhost"))
            {
                state = ConnectState.CONNECTED;  // so the connect screen is drawn
                clc.serverMessage = "";
                cl.gamestate.data.Clear();
                clc.lastPacketSentTime = -9999;
                UpdateScreen();
            }
            else
            {
                CVars.Instance.Set("nextmap", "");
                Disconnect(true);
                servername = "localhost";
                state = ConnectState.CHALLENGING;    // so the connect screen is drawn
                UpdateScreen();
                clc.connectTime = -3000;
                IPEndPoint end = new IPEndPoint(IPAddress.Parse("127.0.0.1"), Net.Instance.net_port.Integer);
                clc.serverAddress = end; // cls.servername FIX
                // we don't need a challenge on the localhost
                CheckForResend();
            }
        }

        /*
        =================
        CL_CheckForResend

        Resend a connect message if the last one has timed out
        =================
        */
        void CheckForResend()
        {
            // resend if we haven't gotten a reply yet
            if (state != ConnectState.CONNECTING && state != ConnectState.CHALLENGING)
            {
                return;
            }

            if (realtime - clc.connectTime < 3000)
            {
                return;
            }

            clc.connectTime = realtime;
            clc.connectPacketCount++;

            if (clc.connectPacketCount == 5)
            {
                Disconnect(true);
                state = ConnectState.DISCONNECTED;
                clc.connectPacketCount = 0;
                Common.Instance.WriteLine("Could not connect: ^1No response from server.");
                return;
            }

            switch (state)
            {
                case ConnectState.CONNECTING:
                    string data = "getchallenge " + clc.challenge;
                    Net.Instance.OutOfBandMessage(Net.NetSource.CLIENT,clc.serverAddress, data);
                    Common.Instance.WriteLine("Connecting{0} to {1}...", (clc.connectPacketCount <= 1) ? "" : "(retry " + clc.connectPacketCount +")", clc.serverAddress.ToString());
                    break;
                case ConnectState.CHALLENGING:
                    // sending back the challenge
                    int port = CVars.Instance.VariableIntegerValue("net_qport");

                    data = "connect ";
                    string cs = CVars.Instance.InfoString(CVarFlags.USER_INFO);
                    cs = Info.SetValueForKey(cs, "qport", ""+port);
                    cs = Info.SetValueForKey(cs, "challenge", ""+clc.challenge);

                    data += '"' + cs + '"';

                    Net.Instance.OutOfBandMessage(Net.NetSource.CLIENT, clc.serverAddress, data);
                    // the most current userinfo has been sent, so watch for any
                    // newer changes to userinfo variables
                    CVars.Instance.modifiedFlags &= ~CVarFlags.USER_INFO;

                    Common.Instance.WriteLine("^8Connection Ok, got challenge.");
                    break;

                default:

                    break;
            }
        }

        public Input.UserCommand GetUserCommand(int cmdNumber)
        {
            // can't return anything that we haven't created yet
            if (cmdNumber > cl.cmdNumber)
            {
                Common.Instance.Error("GetUserCommand: cmdNumber > cl.cmdNumber");
            }

            // the usercmd has been overwritten in the wrapping
            // buffer because it is too far out of date
            if (cmdNumber <= cl.cmdNumber - 64)
                return null;

            return cl.cmds[cmdNumber & 63];
        }

        void CL_LocalServers_f(string[] tokens)
        {
            Common.Instance.WriteLine("Scanning for server on the local network.");

            // Reset list
            localServers.Clear();
            HasNewServers = true;

            // The 'xxx' in the message is a challenge that will be echoed back
            // by the server.  We don't care about that here, but master servers
            // can use that to prevent spoofed server responses from invalid ip
            
            //string message = "\x377\x377\x377\x377getinfo xxx";
            //NetBuffer buf = new NetBuffer(message);
            // send each message twice in case one is dropped
            //for (int i = 0; i < 2; i++)
            //{
                // send a broadcast packet on each server port
                // we support multiple server ports so a single machine
                // can nicely run multiple servers
            NetBuffer buf = new NetBuffer(string.Format("{0}{0}{0}{0}getinfo xxx", 0x377));
            Net.Instance.client.SendOutOfBandMessage(buf, new IPEndPoint(IPAddress.Parse("255.255.255.0"), 27960));
                //for (int j = 0; j < 4; j++)
                //{
                //    Net.Instance.DiscoverLocalServers(27960 + j);
                //}
            //}
        }

        void CL_Connect_f(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                Common.Instance.WriteLine("Connect usage: connect server\n");
                return;
            }

            string server = tokens[1];
            clc.serverMessage = "";

            if (Common.Instance.sv_running.Integer == 1 && server != "localhost")
            {
                // If running a server, shut it down
                Server.Instance.Shutdown("Server quit");
            }

            // Make sure the local server is killed
            CVars.Instance.Set("sv_killserver", "1");
            Server.Instance.Frame(0);

            Disconnect(true);

            IPEndPoint endp = Net.StringToAddr(server);
            if (endp == null)
            {
                Common.Instance.WriteLine("^1Connect failed: Could not lookup {0}", server);
                state = ConnectState.DISCONNECTED;
                return;
            }

            servername = server;
            clc.serverAddress = endp;
            state = ConnectState.CONNECTING;
            clc.connectTime = -99999;
            clc.connectPacketCount = 0;
        }



        internal void ShutdownAll()
        {
            // Shutdown CGame
            CGame.Instance.CG_Shutdown();
            
            // Shutdown renderer
        }
    }
}
