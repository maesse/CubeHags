﻿using System;
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

namespace CubeHags.client
{
    public sealed partial class Client
    {
        private static readonly Client _Instance = new Client();
        public static Client Instance { get { return _Instance; } }
        public RenderForm form;

        public CVar cl_timeout = CVars.Instance.Get("cl_timeout", "200", CVarFlags.NONE);
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

            CVars.Instance.Get("name", "UnknownCube", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("rate", "25000", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("model", "unknown", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("cl_updaterate", "40", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);

            Renderer.Instance.Init(form);
            Input.Instance.InitializeInput();
            HagsConsole.Instance.Init();

            CVars.Instance.Set("cl_running", "1");
            cin = new Cinematic();
            Commands.Instance.AddCommand("cinematic", new CommandDelegate(cin.PlayCinematic_f));
            Commands.Instance.AddCommand("connect", new CommandDelegate(CL_Connect_f));
            Commands.Instance.AddCommand("localservers", new CommandDelegate(CL_LocalServers_f));
            
            Common.Instance.WriteLine("------- Client initialization Complete --------");
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
            // TODO
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
            if (Common.Instance.cl_running.Integer == 0)
            {
                return;
            }

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

            switch (state)
            {
                case ConnectState.CONNECTING:
                    string data = "getchallenge " + clc.challenge;
                    Net.Instance.OutOfBandMessage(Net.NetSource.CLIENT,clc.serverAddress, data);

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
                for (int j = 0; j < 4; j++)
                {
                    Net.Instance.DiscoverLocalServers(27960 + j);
                }
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

            // Split connection string
            string addr;
            int portIndex = server.IndexOf(':');
            int port = 27960;
            if (portIndex > 0)
            {
                int portParse;
                if (int.TryParse(server.Substring(portIndex+1), out portParse))
                {
                    port = portParse;
                    addr = server.Substring(0, portIndex);
                }
                else
                    addr = server;

            }
            else
                addr = server;

            // Do we need a dns lookup?
            IPAddress ip;
            if (!IPAddress.TryParse(addr, out ip))
            {
                IPAddress[] ips = Dns.GetHostAddresses(addr);
                if (ips != null && ips.Length > 0)
                {
                    ip = ips[0];
                }
                else
                {
                    Common.Instance.WriteLine("Bad server address");
                    state = ConnectState.DISCONNECTED;
                    return;
                }
            }

            clc.serverAddress = new IPEndPoint(ip, port);
            state = ConnectState.CONNECTING;
            clc.connectTime = -99999;
            clc.connectPacketCount = 0;
        }



        internal void ShutdownAll()
        {
            // clear renderer, cgame, ui
            //throw new NotImplementedException();
        }
    }
}
