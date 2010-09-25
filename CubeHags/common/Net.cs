using System;
using System.Collections.Generic;
 
using System.Text;
using System.Net;
using CubeHags.client.common;
using Lidgren.Network;
using CubeHags.server;
using CubeHags.client;

namespace CubeHags.common
{
    public sealed partial class Net
    {
        public class Packet
        {
            public Packet(NetBuffer buffer, NetMessageType type, NetConnection sender)
            {
                Buffer = buffer;
                Type = type;
                Sender = sender;
            }

            public Packet(NetBuffer buffer, NetMessageType type, IPEndPoint endPoint)
            {
                Buffer = buffer;
                Type = type;
                _endPoint = endPoint;
            }

            private IPEndPoint _endPoint;
            public IPEndPoint Address { get { if (_endPoint == null) return Sender.RemoteEndpoint; return _endPoint; } }
            public NetConnection Sender;
            public NetMessageType Type;
            public NetBuffer Buffer;
        }

        public enum NetSource
        {
            CLIENT,
            SERVER
        }

        private static readonly Net _Instance = new Net();
        public static Net Instance { get { return _Instance; } }

        public CVar net_ip;
        public CVar net_port;
        public CVar net_enabled;
        public bool IsClientConnected = false;

        bool networkingEnabled = false;

        NetConfiguration svConfig = new NetConfiguration("Cubehags");
        NetServer server;
        NetConfiguration clConfig = new NetConfiguration("Cubehags");
        public NetClient client;

        private List<string> lanStrings = new List<string>();
        public NetBaseStatistics ClientStatistic { get { if (client != null) return client.Statistics; return null; } }

        Net()
        {
            
        }

        public static IPEndPoint StringToAddr(string server)
        {
            // Split connection string
            string addr;
            int portIndex = server.IndexOf(':');
            int port = 27960;
            if (portIndex > 0)
            {
                int portParse;
                if (int.TryParse(server.Substring(portIndex + 1), out portParse))
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
                    Common.Instance.WriteLine("^1StringToAddr: Could not lookup {0}", addr);
                    return null;
                }
            }

            IPEndPoint ep = new IPEndPoint(ip, port);
            return ep;
        }

        // Strips the port number from an server address
        public static string StripPort(string addr)
        {
            int index = addr.IndexOf(':');
            if (index > 0)
                return addr.Substring(0, index);
            return addr;
        }

        public void DiscoverLocalServers(int port) 
        {
            client.DiscoverLocalServers(port);
            
        }

        public void SetAllowDiscovery(bool valu)
        {
            server.Configuration.AnswerDiscoveryRequests =valu;
        }

        public bool IsLanAddress(IPAddress adr)
        {
            if (IPAddress.IsLoopback(adr))
                return true;

            string s = adr.ToString();
            if (s.StartsWith("10."))
                return true;
            if (s.StartsWith("192.168"))
                return true;
            if (s.StartsWith("172.16"))
                return true;

            if (s.StartsWith("127."))
                return true;

            foreach (string lan in lanStrings)
            {
                if (s.StartsWith(lan))
                    return true;
            }

            return false;
        }

        public Packet GetPacket()
        {
            NetBuffer buf = new NetBuffer();
            NetMessageType type;
            NetConnection sender;
            IPEndPoint endpoint;
            
            if (server.ReadMessage(buf, out type, out sender, out endpoint))
            {
                switch (type)
                {
                    case NetMessageType.ConnectionApproval:
                        // Check if we are awainting a new connection:
                        int i;
                        for (i = 0; i < Server.Instance.clients.Count; i++)
                        {
                            // Got first packet from a known client?
                            if (IPEndPoint.Equals(Server.Instance.clients[i].netchan.remoteAddress, sender.RemoteEndpoint)
                                && Server.Instance.clients[i].netchan.incomingSequence == 0)
                            {
                                // Accept connection, and save NetConnection in netchan for later sending
                                Server.Instance.clients[i].netchan.connection = sender;
                                sender.Approve();
                                break;
                            }
                        }
                        // Client connection was not expected.. disapprove
                        if (i == Server.Instance.clients.Count)
                            sender.Disapprove("Not expecting this connection");
                        break;
                    case NetMessageType.StatusChanged:
                        // Lidgren network status messages
                        string str = buf.ReadString();
                        //Common.Instance.WriteLine(str);
                        break;
                    case NetMessageType.Data:
                    case NetMessageType.OutOfBandData:
                        return new Packet(buf, type, endpoint);
                    default:
                        str = buf.ReadString();
                        Common.Instance.WriteLine("Lidgren server: " + str);
                        break;
                }
                
            }
            if (client.ReadMessage(buf, out type, out endpoint))
            {
                switch (type)
                {
                    case NetMessageType.ConnectionRejected:
                        Client.Instance.state = ConnectState.DISCONNECTED;
                        string reason = buf.ReadString();
                        Common.Instance.WriteLine("Server rejected client connection: " + reason);
                        break;
                    case NetMessageType.StatusChanged:
                        string str = buf.ReadString();
                        if (str.Equals("Connected"))
                            IsClientConnected = true;
                        //Common.Instance.WriteLine(str);
                        break;
                    case NetMessageType.Data:
                    case NetMessageType.OutOfBandData:
                        return new Packet(buf, type, endpoint);
                    case NetMessageType.ServerDiscovered:
                        for (int i = 0; i < Client.Instance.localServers.Count; i++)
                        {
                            if (Client.Instance.localServers[i].adr == endpoint)
                                return null;
                        }
                        Common.Instance.WriteLine("Discovered lan server at {0}", endpoint);
                        Client.Instance.localServers.Add(new serverInfo_t() { adr = endpoint });
                        Client.Instance.HasNewServers = true;
                        break;
                    default:
                        str = buf.ReadString();
                        Common.Instance.WriteLine("Lidgren Client: " + str);
                        break;
                }

            }
            return null;
        }

        public void OutOfBandMessage(NetSource src, IPEndPoint dest, string data) 
        {
            if (src == NetSource.CLIENT)
            {
                NetBuffer buf = client.CreateBuffer(data);
                client.SendOutOfBandMessage(buf, dest);
            }
            else if (src == NetSource.SERVER)
            {
                NetBuffer buf = server.CreateBuffer(data);
                server.SendOutOfBandMessage(buf, dest);
            }
        }

        void Config(bool enableNetworking)
        {
            bool stop, start;
            // get any latched changes to cvars
            bool modified = GetCvars();

            if (net_enabled.Integer == 0)
                enableNetworking = false;

            // if enable state is the same and no cvars were modified, we have nothing to do
            if (enableNetworking == networkingEnabled && !modified)
                return;

            if (enableNetworking == networkingEnabled)
            {
                if (enableNetworking)
                {
                    stop = true;
                    start = true;
                }
                else
                {
                    stop = false;
                    start = false;
                }
            }
            else
            {
                if (enableNetworking)
                {
                    start = true;
                    stop = false;
                }
                else
                {
                    stop = true;
                    start = false;
                }
                networkingEnabled = enableNetworking;
            }

            if (stop)
            {
                // Close socket
                CloseSocket();
            }

            if (start && net_enabled.Integer == 1)
            {
                // Open socket

                OpenIP();
            }


        }

        public void Pump()
        {
            client.Pump();
            server.Pump();
        }

        void OpenIP()
        {
            int port = net_port.Integer;

            svConfig.MaxConnections = 64;
            svConfig.Port = port;
            server = new NetServer(svConfig);
            server.Configuration.AnswerDiscoveryRequests = false;
            server.EnabledMessageTypes |= NetMessageType.OutOfBandData;
            //server.SimulatedMinimumLatency = 0.7f;
            server.SetMessageTypeEnabled(NetMessageType.ConnectionApproval, true);
            try
            {
                server.Start();
            }
            catch (NetException ex)
            {
                // Try the next port
                server.Configuration.Port = ++port;
                server.Start();
            }
            client = new NetClient(clConfig);
            //client.SimulatedMinimumLatency = 0.2f;
            //client.RunSleep = 0;
            client.EnabledMessageTypes |= NetMessageType.OutOfBandData;
            client.Start();
        }

        void CloseSocket()
        {
            server.Shutdown("Shutting down networking");
        }

        void Cmd_Restart(string[] tokens)
        {
            Config(networkingEnabled);
        }

        public void Init()
        {
            //System.Net.NetworkInformation.NetworkInterface[] interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();

            //foreach (System.Net.NetworkInformation.NetworkInterface iface in interfaces)
            //{
            //    System.Net.NetworkInformation.IPInterfaceProperties properties = iface.GetIPProperties();

            //    foreach (System.Net.NetworkInformation.UnicastIPAddressInformation address in properties.UnicastAddresses)
            //    {
            //        if (address.Address == null || address.IPv4Mask == null)
            //            continue;
            //        string mask = address.IPv4Mask.ToString();
            //        string ip = address.Address.ToString();
            //        if (!mask.Equals("0.0.0.0") && mask.Length > 0 && ip.Length > 0)
            //        {
            //            string[] tokens = mask.Split('.');
            //            string[] iptokens = ip.Split('.');
            //            if (tokens.Length != iptokens.Length)
            //                continue;
            //            bool val = true;
            //            string lanipval = "";
            //            for (int i = 0; i < tokens.Length; i++)
            //            {
            //                if (val)
            //                {
            //                    if (tokens[i].Equals("255"))
            //                    {
            //                        lanipval += iptokens[i] + '.';
            //                    }
            //                    else
            //                        break;
            //                }
            //            }
            //            if (!lanipval.Equals(""))
            //            {
            //                Common.Instance.WriteLine("Ips starting with '{0}' will be regarded as LAN clients", lanipval);
            //                lanStrings.Add(lanipval);
            //            }
            //        }
            //    }
            //}
            qport = CVars.Instance.Get("net_qport", "" + (new Random().Next(0, 65535)&0xffff), CVarFlags.INIT);
            Config(true);
            Commands.Instance.AddCommand("net_restart", new CommandDelegate(Cmd_Restart));
        }

        public void ClientConnect(IPEndPoint end)
        {
            client.Connect(end);
        }

        public void SendPacket(NetSource sock, NetBuffer data, NetConnection conn)
        {
            
            if (sock == NetSource.CLIENT)
            {
                client.SendMessage(data, NetChannel.Unreliable);
            }
            else
                server.SendMessage(data, conn, NetChannel.Unreliable);
        }

        bool GetCvars()
        {
            bool modified = false;
            net_enabled = CVars.Instance.Get("net_enabled", "1", CVarFlags.ARCHIVE | CVarFlags.LATCH);
            modified = net_enabled.Modified;
            net_enabled.Modified = false;

            net_ip = CVars.Instance.Get("net_ip", "127.0.0.1", CVarFlags.LATCH);
            if (net_ip.Modified)
            {
                modified = true;
                net_ip.Modified = false;
            }

            net_port = CVars.Instance.Get("net_port", "27960", CVarFlags.LATCH);
            if (net_port.Modified)
            {
                modified = true;
                net_port.Modified = false;
            }

            return modified;
        }



        /*
        ==================
        MSG_ReadDeltaEntity

        The entity number has already been read from the message, which
        is how the from state is identified.

        If the delta removes the entity, entityState_t->number will be set to MAX_GENTITIES-1

        Can go from either a baseline or a previous packet_entity
        ==================
        */
        public void MSG_ReadDeltaEntity(NetBuffer msg, ref Common.entityState_t from, ref Common.entityState_t to, int number)
        {
            int startBit = msg.Position-32;
            if (number < 0 || number >= 1024)
            {
                Common.Instance.Error("ReadDeltaEntity: number < 0 || number >= 1024");
            }

            // Check for remove
            if (msg.ReadBoolean())
            {
                to = new Common.entityState_t();
                to.number = 1023;
                Common.Instance.WriteLine("Removed entity: {0}", number);
                return;
            }

            // Check for no delta
            if (!msg.ReadBoolean())
            {
                to = from;
                to.number = number;
                return;
            }
            
            to.number = number;
            int dataStart = msg.Position;
            to.eType = msg.ReadBoolean() ? msg.ReadInt32() : from.eType;
            to.eFlags = msg.ReadBoolean() ? (Common.EntityFlags)msg.ReadInt32() : from.eFlags;
            int middle = msg.Position;
            to.pos.trBase.X = msg.ReadBoolean() ? msg.ReadFloat() : from.pos.trBase.X;
            to.pos.trBase.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.pos.trBase.Y;
            to.pos.trBase.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.pos.trBase.Z;
            to.pos.trDelta.X = msg.ReadBoolean() ? msg.ReadFloat() : from.pos.trDelta.X;
            to.pos.trDelta.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.pos.trDelta.Y;
            to.pos.trDelta.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.pos.trDelta.Z;
            to.pos.trDuration = msg.ReadBoolean() ? msg.ReadInt32() : from.pos.trDuration;
            to.pos.trTime = msg.ReadBoolean() ? msg.ReadInt32() : from.pos.trTime;
            to.pos.trType = msg.ReadBoolean() ? (Common.trType_t)msg.ReadInt32() : from.pos.trType;

            to.apos.trBase.X = msg.ReadBoolean() ? msg.ReadFloat() : from.apos.trBase.X;
            to.apos.trBase.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.apos.trBase.Y;
            to.apos.trBase.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.apos.trBase.Z;
            to.apos.trDelta.X = msg.ReadBoolean() ? msg.ReadFloat() : from.apos.trDelta.X;
            to.apos.trDelta.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.apos.trDelta.Y;
            to.apos.trDelta.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.apos.trDelta.Z;
            to.apos.trDuration = msg.ReadBoolean() ? msg.ReadInt32() : from.apos.trDuration;
            to.apos.trTime = msg.ReadBoolean() ? msg.ReadInt32() : from.apos.trTime;
            to.apos.trType = msg.ReadBoolean() ? (Common.trType_t)msg.ReadInt32() : from.apos.trType;

            to.time = msg.ReadBoolean() ? msg.ReadInt32() : from.time;
            to.time2 = msg.ReadBoolean() ? msg.ReadInt32() : from.time2;
            
            to.origin.X = msg.ReadBoolean() ? msg.ReadFloat() : from.origin.X;
            to.origin.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.origin.Y;
            to.origin.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.origin.Z;
            to.origin2.X = msg.ReadBoolean() ? msg.ReadFloat() : from.origin2.X;
            to.origin2.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.origin2.Y;
            to.origin2.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.origin2.Z;

            to.angles.X = msg.ReadBoolean() ? msg.ReadFloat() : from.angles.X;
            to.angles.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.angles.Y;
            to.angles.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.angles.Z;
            to.angles2.X = msg.ReadBoolean() ? msg.ReadFloat() : from.angles2.X;
            to.angles2.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.angles2.Y;
            to.angles2.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.angles2.Z;
            
            to.otherEntityNum = msg.ReadBoolean() ? msg.ReadInt32() : from.otherEntityNum;
            to.otherEntityNum2 = msg.ReadBoolean() ? msg.ReadInt32() : from.otherEntityNum2;
            to.groundEntityNum = msg.ReadBoolean() ? msg.ReadInt32() : from.groundEntityNum;

            to.modelindex = msg.ReadBoolean() ? msg.ReadInt32() : from.modelindex;
            to.clientNum = msg.ReadBoolean() ? msg.ReadInt32() : from.clientNum;
            to.frame = msg.ReadBoolean() ? msg.ReadInt32() : from.frame;
            to.solid = msg.ReadBoolean() ? msg.ReadInt32() : from.solid;
            to.generic1 = msg.ReadBoolean() ? msg.ReadInt32() : from.generic1;
            int lenghtBits = msg.ReadInt32();

            dataStart = msg.Position - dataStart;
            lenghtBits -= dataStart;
            for (int i = 0; i < lenghtBits; i++)
            {
                msg.ReadBoolean();
            }
            middle = msg.Position - middle;
            
            //Common.Instance.WriteLine("MSG_ReadDeltaEntity: Read {0} bits", msg.Position - startBit);
        }


        /*
        ==================
        MSG_WriteDeltaEntity

        Writes part of a packetentities message, including the entity number.
        Can delta from either a baseline or a previous packet_entity
        If to is NULL, a remove entity update will be sent
        If force is not set, then nothing at all will be generated if the entity is
        identical, under the assumption that the in-order delta code will catch it.
        ==================
        */
        public void MSG_WriteDeltaEntity(NetBuffer msg, ref Common.entityState_t from, ref Common.entityState_t to, bool force)
        {
            int msgStart = msg.LengthBits;
            // a NULL to is a delta remove message
            if (to == null)
            {
                if (from == null)
                    return;
                msg.Write(from.number);
                msg.Write(true);
                return;
            }

            if (to.number < 0 || to.number >= 1024)
            {
                Common.Instance.Error("MSG_WriteDeltaEntity: Bad entity number: " + to.number);
            }

            NetBuffer buf = new NetBuffer();
            //NetBuffer buf = msg;
            int lc = 0;
            //if (from.number != to.number) { lc = 1; buf.Write(true); buf.Write(to.number); } else { buf.Write(false); }
            if (from.eType != to.eType) { lc = 2; buf.Write(true); buf.Write(to.eType); } else { buf.Write(false); }
            if (from.eFlags != to.eFlags) { lc = 3; buf.Write(true); buf.Write((int)to.eFlags); } else { buf.Write(false); }
            int middle = buf.LengthBits;
            if (from.pos.trBase.X != to.pos.trBase.X) { lc = 4; buf.Write(true); buf.Write(to.pos.trBase.X); } else { buf.Write(false); }
            if (from.pos.trBase.Y != to.pos.trBase.Y) { lc = 5; buf.Write(true); buf.Write(to.pos.trBase.Y); } else { buf.Write(false); }
            if (from.pos.trBase.Z != to.pos.trBase.Z) { lc = 6; buf.Write(true); buf.Write(to.pos.trBase.Z); } else { buf.Write(false); }
            if (from.pos.trDelta.X != to.pos.trDelta.X) { 
                lc = 7; buf.Write(true); buf.Write(to.pos.trDelta.X); } else { buf.Write(false); }
            if (from.pos.trDelta.Y != to.pos.trDelta.Y) { lc = 8; buf.Write(true); buf.Write(to.pos.trDelta.Y); } else { buf.Write(false); }
            if (from.pos.trDelta.Z != to.pos.trDelta.Z) { lc = 9; buf.Write(true); buf.Write(to.pos.trDelta.Z); } else { buf.Write(false); }
            if (from.pos.trDuration != to.pos.trDuration) { lc = 10; buf.Write(true); buf.Write(to.pos.trDuration); } else { buf.Write(false); }
            if (from.pos.trTime != to.pos.trTime) { lc = 11; buf.Write(true); buf.Write(to.pos.trTime); } else { buf.Write(false); }
            if (from.pos.trType != to.pos.trType) { lc = 12; buf.Write(true); buf.Write((int)to.pos.trType); } else { buf.Write(false); }

            if (from.apos.trBase.X != to.apos.trBase.X) { lc = 13; buf.Write(true); buf.Write(to.apos.trBase.X); } else { buf.Write(false); }
            if (from.apos.trBase.Y != to.apos.trBase.Y) { lc = 14; buf.Write(true); buf.Write(to.apos.trBase.Y); } else { buf.Write(false); }
            if (from.apos.trBase.Z != to.apos.trBase.Z) { lc = 15; buf.Write(true); buf.Write(to.apos.trBase.Z); } else { buf.Write(false); }
            if (from.apos.trDelta.X != to.apos.trDelta.X) { lc = 16; buf.Write(true); buf.Write(to.apos.trDelta.X); } else { buf.Write(false); }
            if (from.apos.trDelta.Y != to.apos.trDelta.Y) { lc = 17; buf.Write(true); buf.Write(to.apos.trDelta.Y); } else { buf.Write(false); }
            if (from.apos.trDelta.Z != to.apos.trDelta.Z) { lc = 18; buf.Write(true); buf.Write(to.apos.trDelta.Z); } else { buf.Write(false); }
            if (from.apos.trDuration != to.apos.trDuration) { lc = 19; buf.Write(true); buf.Write(to.apos.trDuration); } else { buf.Write(false); }
            if (from.apos.trTime != to.apos.trTime) { lc = 20; buf.Write(true); buf.Write(to.apos.trTime); } else { buf.Write(false); }
            if (from.apos.trType != to.apos.trType) { lc = 21; buf.Write(true); buf.Write((int)to.apos.trType); } else { buf.Write(false); }


            if (from.time != to.time) { lc = 22; buf.Write(true); buf.Write(to.time); } else { buf.Write(false); }
            if (from.time2 != to.time2) { lc = 23; buf.Write(true); buf.Write(to.time2); } else { buf.Write(false); }
            
            if (from.origin.X != to.origin.X) { lc = 24; buf.Write(true); buf.Write(to.origin.X); } else { buf.Write(false); }
            if (from.origin.Y != to.origin.Y) { lc = 25; buf.Write(true); buf.Write(to.origin.Y); } else { buf.Write(false); }
            if (from.origin.Z != to.origin.Z) { lc = 26; buf.Write(true); buf.Write(to.origin.Z); } else { buf.Write(false); }
            if (from.origin2.X != to.origin2.X) { lc = 27; buf.Write(true); buf.Write(to.origin2.X); } else { buf.Write(false); }
            if (from.origin2.Y != to.origin2.Y) { lc = 28; buf.Write(true); buf.Write(to.origin2.Y); } else { buf.Write(false); }
            if (from.origin2.Z != to.origin2.Z) { lc = 29; buf.Write(true); buf.Write(to.origin2.Z); } else { buf.Write(false); }

            if (from.angles.X != to.angles.X) { lc = 30; buf.Write(true); buf.Write(to.angles.X); } else { buf.Write(false); }
            if (from.angles.Y != to.angles.Y) { lc = 31; buf.Write(true); buf.Write(to.angles.Y); } else { buf.Write(false); }
            if (from.angles.Z != to.angles.Z) { lc = 32; buf.Write(true); buf.Write(to.angles.Z); } else { buf.Write(false); }
            if (from.angles2.X != to.angles2.X) { lc = 33; buf.Write(true); buf.Write(to.angles2.X); } else { buf.Write(false); }
            if (from.angles2.Y != to.angles2.Y) { lc = 34; buf.Write(true); buf.Write(to.angles2.Y); } else { buf.Write(false); }
            if (from.angles2.Z != to.angles2.Z) { lc = 35; buf.Write(true); buf.Write(to.angles2.Z); } else { buf.Write(false); }
            
            if (from.otherEntityNum != to.otherEntityNum) { lc = 36; buf.Write(true); buf.Write(to.otherEntityNum); } else { buf.Write(false); }
            if (from.otherEntityNum2 != to.otherEntityNum2) { lc = 37; buf.Write(true); buf.Write(to.otherEntityNum2); } else { buf.Write(false); }

            if (from.groundEntityNum != to.groundEntityNum) { lc = 38; buf.Write(true); buf.Write(to.groundEntityNum); } else { buf.Write(false); }

            if (from.modelindex != to.modelindex) { lc = 39; buf.Write(true); buf.Write(to.modelindex); } else { buf.Write(false); }
            if (from.clientNum != to.clientNum) { lc = 40; buf.Write(true); buf.Write(to.clientNum); } else { buf.Write(false); }
            if (from.frame != to.frame) { lc = 41; buf.Write(true); buf.Write(to.frame); } else { buf.Write(false); }

            if (from.solid != to.solid) { lc = 42; buf.Write(true); buf.Write(to.solid); } else { buf.Write(false); }
            if (from.generic1 != to.generic1) { lc = 43; buf.Write(true); buf.Write(to.generic1); } else { buf.Write(false); }

            if (lc == 0)
            {
                // nothing at all changed
                if (!force)
                {
                    return;		// nothing at all
                }
                // write two bits for no change
                msg.Write(to.number);
                msg.Write(false);   // not removed
                msg.Write(false);   // no delta
                return;
            }

            msg.Write(to.number);
            msg.Write(false);   // not removed
            msg.Write(true);    // we have a delta

            
            
            //msg.Write(lc);  // # of changes
            //msg.CopyFrom(buf);
            int msgPos = msg.LengthBits;
            //msg.Write(buf.Data);
            WriteDeltaEntityHags(msg, ref  from,ref  to);
            msg.Write(buf.LengthBits);

            //Common.Instance.WriteLine("MSG_WriteDeltaEntity: Wrote {0} bits", msg.LengthBits - msgStart);

        }
        public static void WriteDeltaEntityHags(NetBuffer buf, ref Common.entityState_t from, ref Common.entityState_t to)
        {
            int lc = 0;
            if (from.eType != to.eType) { lc = 2; buf.Write(true); buf.Write(to.eType); } else { buf.Write(false); }
            if (from.eFlags != to.eFlags) { lc = 3; buf.Write(true); buf.Write((int)to.eFlags); } else { buf.Write(false); }
            int middle = buf.LengthBits;
            if (from.pos.trBase.X != to.pos.trBase.X) { lc = 4; buf.Write(true); buf.Write(to.pos.trBase.X); } else { buf.Write(false); }
            if (from.pos.trBase.Y != to.pos.trBase.Y) { lc = 5; buf.Write(true); buf.Write(to.pos.trBase.Y); } else { buf.Write(false); }
            if (from.pos.trBase.Z != to.pos.trBase.Z) { lc = 6; buf.Write(true); buf.Write(to.pos.trBase.Z); } else { buf.Write(false); }
            if (from.pos.trDelta.X != to.pos.trDelta.X) { lc = 7; buf.Write(true); buf.Write(to.pos.trDelta.X); } else { buf.Write(false); }
            if (from.pos.trDelta.Y != to.pos.trDelta.Y) { lc = 8; buf.Write(true); buf.Write(to.pos.trDelta.Y); } else { buf.Write(false); }
            if (from.pos.trDelta.Z != to.pos.trDelta.Z) { lc = 9; buf.Write(true); buf.Write(to.pos.trDelta.Z); } else { buf.Write(false); }
            if (from.pos.trDuration != to.pos.trDuration) { lc = 10; buf.Write(true); buf.Write(to.pos.trDuration); } else { buf.Write(false); }
            if (from.pos.trTime != to.pos.trTime) { lc = 11; buf.Write(true); buf.Write(to.pos.trTime); } else { buf.Write(false); }
            if (from.pos.trType != to.pos.trType) { lc = 12; buf.Write(true); buf.Write((int)to.pos.trType); } else { buf.Write(false); }

            if (from.apos.trBase.X != to.apos.trBase.X) { lc = 13; buf.Write(true); buf.Write(to.apos.trBase.X); } else { buf.Write(false); }
            if (from.apos.trBase.Y != to.apos.trBase.Y) { lc = 14; buf.Write(true); buf.Write(to.apos.trBase.Y); } else { buf.Write(false); }
            if (from.apos.trBase.Z != to.apos.trBase.Z) { lc = 15; buf.Write(true); buf.Write(to.apos.trBase.Z); } else { buf.Write(false); }
            if (from.apos.trDelta.X != to.apos.trDelta.X) { lc = 16; buf.Write(true); buf.Write(to.apos.trDelta.X); } else { buf.Write(false); }
            if (from.apos.trDelta.Y != to.apos.trDelta.Y) { lc = 17; buf.Write(true); buf.Write(to.apos.trDelta.Y); } else { buf.Write(false); }
            if (from.apos.trDelta.Z != to.apos.trDelta.Z) { lc = 18; buf.Write(true); buf.Write(to.apos.trDelta.Z); } else { buf.Write(false); }
            if (from.apos.trDuration != to.apos.trDuration) { lc = 19; buf.Write(true); buf.Write(to.apos.trDuration); } else { buf.Write(false); }
            if (from.apos.trTime != to.apos.trTime) { lc = 20; buf.Write(true); buf.Write(to.apos.trTime); } else { buf.Write(false); }
            if (from.apos.trType != to.apos.trType) { lc = 21; buf.Write(true); buf.Write((int)to.apos.trType); } else { buf.Write(false); }


            if (from.time != to.time) { lc = 22; buf.Write(true); buf.Write(to.time); } else { buf.Write(false); }
            if (from.time2 != to.time2) { lc = 23; buf.Write(true); buf.Write(to.time2); } else { buf.Write(false); }

            if (from.origin.X != to.origin.X) { lc = 24; buf.Write(true); buf.Write(to.origin.X); } else { buf.Write(false); }
            if (from.origin.Y != to.origin.Y) { lc = 25; buf.Write(true); buf.Write(to.origin.Y); } else { buf.Write(false); }
            if (from.origin.Z != to.origin.Z) { lc = 26; buf.Write(true); buf.Write(to.origin.Z); } else { buf.Write(false); }
            if (from.origin2.X != to.origin2.X) { lc = 27; buf.Write(true); buf.Write(to.origin2.X); } else { buf.Write(false); }
            if (from.origin2.Y != to.origin2.Y) { lc = 28; buf.Write(true); buf.Write(to.origin2.Y); } else { buf.Write(false); }
            if (from.origin2.Z != to.origin2.Z) { lc = 29; buf.Write(true); buf.Write(to.origin2.Z); } else { buf.Write(false); }

            if (from.angles.X != to.angles.X) { lc = 30; buf.Write(true); buf.Write(to.angles.X); } else { buf.Write(false); }
            if (from.angles.Y != to.angles.Y) { lc = 31; buf.Write(true); buf.Write(to.angles.Y); } else { buf.Write(false); }
            if (from.angles.Z != to.angles.Z) { lc = 32; buf.Write(true); buf.Write(to.angles.Z); } else { buf.Write(false); }
            if (from.angles2.X != to.angles2.X) { lc = 33; buf.Write(true); buf.Write(to.angles2.X); } else { buf.Write(false); }
            if (from.angles2.Y != to.angles2.Y) { lc = 34; buf.Write(true); buf.Write(to.angles2.Y); } else { buf.Write(false); }
            if (from.angles2.Z != to.angles2.Z) { lc = 35; buf.Write(true); buf.Write(to.angles2.Z); } else { buf.Write(false); }

            if (from.otherEntityNum != to.otherEntityNum) { lc = 36; buf.Write(true); buf.Write(to.otherEntityNum); } else { buf.Write(false); }
            if (from.otherEntityNum2 != to.otherEntityNum2) { lc = 37; buf.Write(true); buf.Write(to.otherEntityNum2); } else { buf.Write(false); }

            if (from.groundEntityNum != to.groundEntityNum) { lc = 38; buf.Write(true); buf.Write(to.groundEntityNum); } else { buf.Write(false); }

            if (from.modelindex != to.modelindex) { lc = 39; buf.Write(true); buf.Write(to.modelindex); } else { buf.Write(false); }
            if (from.clientNum != to.clientNum) { lc = 40; buf.Write(true); buf.Write(to.clientNum); } else { buf.Write(false); }
            if (from.frame != to.frame) { lc = 41; buf.Write(true); buf.Write(to.frame); } else { buf.Write(false); }

            if (from.solid != to.solid) { lc = 42; buf.Write(true); buf.Write(to.solid); } else { buf.Write(false); }
            if (from.generic1 != to.generic1) { lc = 43; buf.Write(true); buf.Write(to.generic1); } else { buf.Write(false); }
        }
        public static void ReadDeltaPlayerstate(NetBuffer msg, Common.PlayerState from, Common.PlayerState to)
        {
            int startoffset = msg.Position;
            if (from == null)
                from = new Common.PlayerState();

            to.commandTime = msg.ReadBoolean() ? msg.ReadInt32() : from.commandTime;
            to.pm_type = msg.ReadBoolean() ? (Common.PMType)msg.ReadInt32() : from.pm_type;
            to.pm_flags = msg.ReadBoolean() ? (client.PMFlags)msg.ReadInt32() : from.pm_flags;
            to.pm_time = msg.ReadBoolean() ? msg.ReadInt32() : from.pm_time;
            to.origin.X = msg.ReadBoolean() ? msg.ReadFloat() : from.origin.X;
            to.origin.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.origin.Y;
            to.origin.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.origin.Z;
            to.velocity.X = msg.ReadBoolean() ? msg.ReadFloat() : from.velocity.X;
            to.velocity.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.velocity.Y;
            to.velocity.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.velocity.Z;
            to.weaponTime = msg.ReadBoolean() ? msg.ReadInt32() : from.weaponTime;
            to.gravity = msg.ReadBoolean() ? msg.ReadInt32() : from.gravity;
            to.delta_angles[0] = msg.ReadBoolean() ? msg.ReadInt32() : from.delta_angles[0];
            to.delta_angles[1] = msg.ReadBoolean() ? msg.ReadInt32() : from.delta_angles[1];
            to.delta_angles[2] = msg.ReadBoolean() ? msg.ReadInt32() : from.delta_angles[2];
            to.groundEntityNum = msg.ReadBoolean() ? msg.ReadInt32() : from.groundEntityNum;
            to.movementDir = msg.ReadBoolean() ? msg.ReadInt32() : from.movementDir;
            to.grapplePoint.X = msg.ReadBoolean() ? msg.ReadFloat() : from.grapplePoint.X;
            to.grapplePoint.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.grapplePoint.Y;
            to.grapplePoint.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.grapplePoint.Z;
            to.speed = msg.ReadBoolean() ? msg.ReadInt32() : from.speed;
            to.eFlags = msg.ReadBoolean() ? (Common.EntityFlags)Enum.Parse(typeof(Common.EntityFlags), ""+msg.ReadInt32()) : from.eFlags;
            to.eventSequence = msg.ReadBoolean() ? msg.ReadInt32() : from.eventSequence;
            to.events[0] = msg.ReadBoolean() ? msg.ReadInt32() : from.events[0];
            to.events[1] = msg.ReadBoolean() ? msg.ReadInt32() : from.events[1];
            to.eventParms[0] = msg.ReadBoolean() ? msg.ReadInt32() : from.eventParms[0];
            to.eventParms[1] = msg.ReadBoolean() ? msg.ReadInt32() : from.eventParms[1];
            to.externalEvent = msg.ReadBoolean() ? msg.ReadInt32() : from.externalEvent;
            to.externalEventParm = msg.ReadBoolean() ? msg.ReadInt32() : from.externalEventParm;
            to.externalEventTime = msg.ReadBoolean() ? msg.ReadInt32() : from.externalEventTime;
            to.clientNum = msg.ReadBoolean() ? msg.ReadInt32() : from.clientNum;
            to.viewangles.X = msg.ReadBoolean() ? msg.ReadFloat() : from.viewangles.X;
            to.viewangles.Y = msg.ReadBoolean() ? msg.ReadFloat() : from.viewangles.Y;
            to.viewangles.Z = msg.ReadBoolean() ? msg.ReadFloat() : from.viewangles.Z;
            to.viewheight = msg.ReadBoolean() ? msg.ReadInt32() : from.viewheight;
            to.generic1 = msg.ReadBoolean() ? msg.ReadInt32() : from.generic1;
            
            // Got diff arrays

            int msgMiddle = 99999;
            if (msg.ReadBoolean())
            {
                if (msg.ReadBoolean())
                {
                    // stat
                    int statbits = msg.ReadInt32();
                    for (int i = 0; i < 16; i++)
                    {
                        if ((statbits & (1 << i)) == (1 << i))
                        {
                            to.stats[i] = msg.ReadInt16();
                        }
                        else
                            to.stats[i] = from.stats[i];
                    }
                }
                else
                    to.stats = from.stats;
                msgMiddle = msg.Position;
                if (msg.ReadBoolean())
                {
                    // pers
                    int persbits = msg.ReadInt32();
                    for (int i = 0; i < 16; i++)
                    {
                        if ((persbits & (1 << i)) == (1 << i))
                        {
                            to.persistant[i] = msg.ReadInt16();
                        }
                        else
                            to.persistant[i] = from.persistant[i];
                    }
                }
                else
                    to.persistant = from.persistant;
            }
            else
            {
                to.stats = from.stats;
                to.persistant = from.persistant;
            }

            //System.Console.WriteLine("Read {0}bits snapshot, {1} middle", msg.Position - startoffset, msgMiddle - startoffset);

        }

        public static void WriteDeltaPlayerstate(NetBuffer msg, Common.PlayerState from, Common.PlayerState to)
        {
            int msgStart = msg.LengthBits;
            if (from == null)
                from = new Common.PlayerState();


            if (from.commandTime != to.commandTime) { msg.Write(true); msg.Write(to.commandTime); } else { msg.Write(false); }
            if (from.pm_type != to.pm_type) { msg.Write(true); msg.Write((int)to.pm_type); } else { msg.Write(false); }
            if (from.pm_flags != to.pm_flags) { msg.Write(true); msg.Write((int)to.pm_flags); } else { msg.Write(false); }
            if (from.pm_time != to.pm_time) { msg.Write(true); msg.Write(to.pm_time); } else { msg.Write(false); }
            if (from.origin.X != to.origin.X) { msg.Write(true); msg.Write(to.origin.X); } else { msg.Write(false); }
            if (from.origin.Y != to.origin.Y)
            { msg.Write(true); msg.Write(to.origin.Y); }
            else
            { msg.Write(false); }
            if (from.origin.Z != to.origin.Z) { msg.Write(true); msg.Write(to.origin.Z); } else { msg.Write(false); }
            if (from.velocity.X != to.velocity.X) { msg.Write(true); msg.Write(to.velocity.X); } else { msg.Write(false); }
            if (from.velocity.Y != to.velocity.Y) { msg.Write(true); msg.Write(to.velocity.Y); } else { msg.Write(false); }
            if (from.velocity.Z != to.velocity.Z) { msg.Write(true); msg.Write(to.velocity.Z); } else { msg.Write(false); }

            if (from.weaponTime != to.weaponTime) { msg.Write(true); msg.Write(to.weaponTime); } else { msg.Write(false); }
            if (from.gravity != to.gravity) { msg.Write(true); msg.Write(to.gravity); } else { msg.Write(false); }
            if (from.delta_angles[0] != to.delta_angles[0]) { msg.Write(true); msg.Write(to.delta_angles[0]); } else { msg.Write(false); }
            if (from.delta_angles[1] != to.delta_angles[1]) { msg.Write(true); msg.Write(to.delta_angles[1]); } else { msg.Write(false); }
            if (from.delta_angles[2] != to.delta_angles[2]) { msg.Write(true); msg.Write(to.delta_angles[2]); } else { msg.Write(false); }
            if (from.groundEntityNum != to.groundEntityNum) { msg.Write(true); msg.Write(to.groundEntityNum); } else { msg.Write(false); }
            if (from.movementDir != to.movementDir) { msg.Write(true); msg.Write(to.movementDir); } else { msg.Write(false); }
            if (from.grapplePoint.X != to.grapplePoint.X) { msg.Write(true); msg.Write(to.grapplePoint.X); } else { msg.Write(false); }
            if (from.grapplePoint.Y != to.grapplePoint.Y) { msg.Write(true); msg.Write(to.grapplePoint.Y); } else { msg.Write(false); }
            if (from.grapplePoint.Z != to.grapplePoint.Z) { msg.Write(true); msg.Write(to.grapplePoint.Z); } else { msg.Write(false); }
            if (from.speed != to.speed) { msg.Write(true); msg.Write(from.speed); } else { msg.Write(false); }

            if (from.eFlags != to.eFlags) { msg.Write(true); msg.Write((int)to.eFlags); } else { msg.Write(false); }
            if (from.eventSequence != to.eventSequence) { msg.Write(true); msg.Write(to.eventSequence); } else { msg.Write(false); }
            if (from.events[0] != to.events[0]) { msg.Write(true); msg.Write(to.events[0]); } else { msg.Write(false); }
            if (from.events[1] != to.events[1]) { msg.Write(true); msg.Write(to.events[1]); } else { msg.Write(false); }
            if (from.eventParms[0] != to.eventParms[0]) { msg.Write(true); msg.Write(to.eventParms[0]); } else { msg.Write(false); }
            if (from.eventParms[1] != to.eventParms[1]) { msg.Write(true); msg.Write(to.eventParms[1]); } else { msg.Write(false); }
            if (from.externalEvent != to.externalEvent) { msg.Write(true); msg.Write(to.externalEvent); } else { msg.Write(false); }
            if (from.externalEventParm != to.externalEventParm) { msg.Write(true); msg.Write(to.externalEventParm); } else { msg.Write(false); }
            if (from.externalEventTime != to.externalEventTime) { msg.Write(true); msg.Write(to.externalEventTime); } else { msg.Write(false); }
            if (from.clientNum != to.clientNum) { msg.Write(true); msg.Write(to.clientNum); } else { msg.Write(false); }
            if (from.viewangles.X != to.viewangles.X) { msg.Write(true); msg.Write(to.viewangles.X); } else { msg.Write(false); }
            if (from.viewangles.Y != to.viewangles.Y) { msg.Write(true); msg.Write(to.viewangles.Y); } else { msg.Write(false); }
            if (from.viewangles.Z != to.viewangles.Z) { msg.Write(true); msg.Write(to.viewangles.Z); } else { msg.Write(false); }
            if (from.viewheight != to.viewheight) { msg.Write(true); msg.Write(to.viewheight); } else { msg.Write(false); }
            if (from.generic1 != to.generic1) { msg.Write(true); msg.Write(to.generic1); } else { msg.Write(false); }
            
            //
            // send the arrays
            //
            int statbits = 0;
            for (int i = 0; i < 16; i++)
            {
                if (from.stats[i] != to.stats[i])
                    statbits |= 1 << i;
            }

            int persbits = 0;
            for (int i = 0; i < 16; i++)
            {
                if (from.persistant[i] != to.persistant[i])
                    persbits |= 1 << i;
            }

            if (persbits == statbits && statbits == 0)
            {
                // no change
                msg.Write(false);
                return;
            }
            msg.Write(true);
            if (statbits > 0)
            {
                msg.Write(true);
                msg.Write(statbits);
                for (int i = 0; i < 16; i++)
                {
                    if ((statbits & (1 << i)) == (1 << i))
                    {
                        msg.Write((short)to.stats[i]);
                    }
                }
            }
            else
                msg.Write(false);
            int msgMiddle = msg.LengthBits;
            if (persbits > 0)
            {
                msg.Write(true);
                msg.Write(persbits);
                for (int i = 0; i < 16; i++)
                {
                    if ((persbits & (1 << i)) == (1 << i))
                    {
                        msg.Write((short)to.persistant[i]);
                    }
                }
            }
            else
                msg.Write(false);

           // System.Console.WriteLine("Wrote {0}bits snapshot, {1} middle", msg.LengthBits - msgStart, msgMiddle - msgStart);
        }
    }

    


    public enum ConnectState : int
    {
        NONE = 0,
        UNINITIALIZED,
    	DISCONNECTED, 	// not talking to a server
    	AUTHORIZING,		// not used any more, was checking cd key 
    	CONNECTING,		// sending request packets to the server
    	CHALLENGING,		// sending challenge packets to the server
    	CONNECTED,		// netchan_t established, getting gamestate
    	LOADING,			// only during cgame initialization, never during main loop
    	PRIMED,			// got gamestate, waiting for first frame
    	ACTIVE,			// game views should be displayed
        CINEMATIC
    }


    //
    // server to client
    //
    public enum svc_ops_e : int
    {
        svc_bad,
        svc_nop,
        svc_gamestate,
        svc_configstring,			// [short] [string] only in gamestate messages
        svc_baseline,				// only in gamestate messages
        svc_serverCommand,			// [string] to be executed by client game module
        svc_download,				// [short] size [size bytes]
        svc_snapshot,
        svc_EOF,

        // svc_extension follows a svc_EOF, followed by another svc_* ...
        //  this keeps legacy clients compatible.
        svc_extension,
        svc_voip,     // not wrapped in USE_VOIP, so this value is reserved.
    }

    //
    // client to server
    //
    public enum clc_ops_e : int
    {
        clc_bad,
        clc_nop,
        clc_move,				// [[usercmd_t]
        clc_moveNoDelta,		// [[usercmd_t]
        clc_clientCommand,		// [string] message
        clc_EOF,

        // clc_extension follows a clc_EOF, followed by another clc_* ...
        //  this keeps legacy servers compatible.
        clc_extension,
        clc_voip,   // not wrapped in USE_VOIP, so this value is reserved.
    }

    
}
