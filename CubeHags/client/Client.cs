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

namespace CubeHags.client
{
    public sealed partial class Client
    {
        private static readonly Client _Instance = new Client();
        public static Client Instance { get { return _Instance; } }
        public RenderForm form;

        public CVar cl_timeout = CVars.Instance.Get("cl_timeout", "200", CVarFlags.NONE);
        public CVar cl_maxpackets = CVars.Instance.Get("cl_maxpackets", "30", CVarFlags.ARCHIVE);
        public CVar cl_packetdup = CVars.Instance.Get("cl_packetdup", "1", CVarFlags.ARCHIVE);
        public CVar cl_timeNudge = CVars.Instance.Get("cl_timeNudge", "0", CVarFlags.TEMP);
        public CVar cl_sensitivity = CVars.Instance.Get("cl_sensitivity", "1", CVarFlags.ARCHIVE);
        public CVar cl_nodelta = CVars.Instance.Get("cl_nodelta", "0", CVarFlags.NONE);

        public clientActive cl = new clientActive();
        public clientStatic_t cls = new clientStatic_t();
        public clientConnect clc = new clientConnect();

        public Cinematic cin;

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
             
            clc.lastPacketTime = cls.realtime;

            if (packet.Type == NetMessageType.OutOfBandData)
            {
                ConnectionlessPacket(packet);
                return;
            }

            if (cls.state < connstate_t.CONNECTED)
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
            clc.lastPacketTime = cls.realtime;
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
                if (cls.state != connstate_t.CONNECTING)
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
                cls.state = connstate_t.CHALLENGING;
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
                if ((int)cls.state >= (int)connstate_t.CONNECTED)
                {
                    Common.Instance.WriteLine("Duplicate connect recieved. Ignored");
                    return;
                }

                if (cls.state != connstate_t.CHALLENGING)
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
                cls.state = connstate_t.CONNECTED;
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
            cls.realFrametime = (int)msec;
            cls.frametime = (int)msec;
            cls.realtime += cls.frametime;

            // see if we need to update any userinfo
            CheckUserInfo();

            // if we haven't gotten a packet in a long time,
            // drop the connection
            CheckTimeout();

            // send intentions now
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
            cls.framecount++;
        }

        

        

        public void Init()
        {
            Common.Instance.WriteLine("------- Client initialization --------");
            
            // Console init


            form = new RenderForm("CubeHags")
            {
                ClientSize = Renderer.Instance.RenderSize
            };
            Input.Instance.InitializeInput();

            CVars.Instance.Get("name", "UnknownCube", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("rate", "25000", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("model", "unknown", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("snaps", "60", CVarFlags.USER_INFO | CVarFlags.ARCHIVE);

            Renderer.Instance.Init(form);

            CVars.Instance.Set("cl_running", "1");
            Common.Instance.WriteLine("------- Client initialization Complete --------");
            cin = new Cinematic();
            cin.AlterGameState = true;
            cin.PlayCinematic("testvid.avi", 0, 0, Renderer.Instance.RenderSize.Width, Renderer.Instance.RenderSize.Height);
            CVars.Instance.Set("nextmap", "map cs_office");
        }

        void CheckTimeout()
        {
            if (cls.state >= connstate_t.CONNECTED && cls.state != connstate_t.CINEMATIC && cls.realtime - clc.lastPacketTime > cl_timeout.Value * 1000)
            {
                if (++cl.timeoutCount > 5)
                {
                    Common.Instance.WriteLine("\nServer connection timed out.");
                    Disonnect(true);
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
        private void Disonnect(bool showMainMenu)
        {
            if (Common.Instance.cl_running.Integer == 0)
            {
                return;
            }

            // send a disconnect message to the server
            // send it a few times in case one is dropped
            if ((int)cls.state >= (int)connstate_t.CONNECTED)
            {
                AddReliableCommand("disconnect", true);
                Input.Instance.WritePacket();
                Input.Instance.WritePacket();
                Input.Instance.WritePacket();
            }

            ClearState();

            // wipe the client connection
            clc = new clientConnect();
            cls.state = connstate_t.DISCONNECTED;
        }

        void CheckUserInfo()
        {
            // don't add reliable commands when not yet connected
            if ((int)cls.state < (int)connstate_t.CHALLENGING)
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
            if ((int)cls.state >= (int)connstate_t.CONNECTED && cls.servername.Equals("localhost"))
            {
                cls.state = connstate_t.CONNECTED;  // so the connect screen is drawn
                cls.updateInfoString = "";
                clc.serverMessage = "";
                cl.gamestate.data.Clear();
                clc.lastPacketSentTime = -9999;
                UpdateScreen();
            }
            else
            {
                CVars.Instance.Set("nextmap", "");
                Disonnect(true);
                cls.servername = "localhost";
                cls.state = connstate_t.CHALLENGING;    // so the connect screen is drawn
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
            if (cls.state != connstate_t.CONNECTING && cls.state != connstate_t.CHALLENGING)
            {
                return;
            }

            if (cls.realtime - clc.connectTime < 3000)
            {
                return;
            }

            clc.connectTime = cls.realtime;
            clc.connectPacketCount++;

            switch (cls.state)
            {
                case connstate_t.CONNECTING:
                    string data = "getchallenge " + clc.challenge;
                    Net.Instance.OutOfBandMessage(Net.NetSource.CLIENT,clc.serverAddress, data);

                    break;
                case connstate_t.CHALLENGING:
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

        void CL_Connect_f(string[] tokens)
        {
            if (tokens.Length != 2)
            {
                Common.Instance.WriteLine("Connect usage: connect server\n");
                return;
            }

            string server = tokens[1];

        }

        internal void ShutdownAll()
        {
            // clear renderer, cgame, ui
            //throw new NotImplementedException();
        }
    }
}
