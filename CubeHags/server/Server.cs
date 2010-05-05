using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.common;
using CubeHags.common;
using CubeHags.client;
using System.Net;
using CubeHags.client.map.Source;
using SlimDX;
using Lidgren.Network;

namespace CubeHags.server
{
    public sealed partial class Server
    {
        private static readonly Server _Instance = new Server();
        public static Server Instance { get { return _Instance; } }
        CVar sv_fps;
        CVar sv_timeout;
        CVar sv_hostname;
        CVar sv_mapname;
        CVar sv_mapChecksum;
        CVar sv_maxclients;
        CVar sv_zombietime;
        public server_t sv = new server_t();
        public serverStatic_t svs;
        worldSector_t[]	sv_worldSectors = new worldSector_t[64];
        int			sv_numworldSectors = 0;

        /*
       =================
       SV_PacketEvent

       A packet has arrived from the main event loop
       =================
       */
        public void PacketEvent(Net.Packet packet)
        {
            // check for connectionless packet (0xffffffff) first
            if (packet.Type == Lidgren.Network.NetMessageType.OutOfBandData)
            {
                ConnectionLessPacket(packet);
                return;
            }

            // read the qport out of the message so we can fix up
            // stupid address translating routers
            NetBuffer buf = packet.Buffer;
            buf.ReadInt32();
            int qport = buf.ReadInt16() & 0xffff;

            // find which client the message is from
            for (int i = 0; i < svs.clients.Count; i++)
            {
                client_t client = svs.clients[i];
                if (client.state == clientState_t.CS_FREE)
                    continue;

                if (!packet.Address.Address.Equals(client.netchan.remoteAddress.Address))
                    continue;

                // it is possible to have multiple clients from a single IP
                // address, so they are differentiated by the qport variable
                if (client.netchan.qport != qport)
                {
                    continue;
                }

                // the IP port can't be used to differentiate them, because
                // some address translating routers periodically change UDP
                // port assignments
                if (client.netchan.remoteAddress.Port != packet.Address.Port)
                {
                    Common.Instance.WriteLine("PacketEvent: fixing up translation port");
                    client.netchan.remoteAddress.Port = packet.Address.Port;
                }

                // make sure it is a valid, in sequence packet
                //??
                // zombie clients still need to do the Netchan_Process
                // to make sure they don't need to retransmit the final
                // reliable message, but they don't do any other processing
                if (client.state != clientState_t.CS_ZOMBIE)
                {
                    client.lastPacketTime = svs.time; // don't timeout
                    ExecuteClientMessage(client, buf);
                }
                return;
            }

            // if we received a sequenced packet from an address we don't recognize,
            // send an out of band disconnect packet to it
            Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, packet.Address, "disconnect");
        }

        void NetChan_Transmit(client_t cl, NetBuffer msg)
        {
            msg.Write((byte)svc_ops_e.svc_EOF);
            Net.Instance.NetChan_Transmit(cl.netchan, msg);
        }

        public Input.UserCommand GetUsercmd(int clientNum)
        {
            if (clientNum < 0 || clientNum >= sv_maxclients.Integer)
                Common.Instance.Error("GetUsercmd: Bad clientNum");
            return svs.clients[clientNum].lastUsercmd;
        }

        /*
        =================
        SV_ConnectionlessPacket

        A connectionless packet has four leading 0xff
        characters to distinguish it from a game channel.
        Clients that are in the game can still send
        connectionless packets.
        =================
        */
        void ConnectionLessPacket(Net.Packet packet)
        {
            NetBuffer buf = packet.Buffer;
            
            string data = buf.ReadString();
            Common.Instance.WriteLine("SV_ConnLessPacket: {0}=>{1}", packet.Address, data);
            string[] tokens = Commands.TokenizeString(data);

            string c = tokens[0];
            if (c.Equals("getchallenge"))
            {
                GetChallenge(packet.Address, tokens[1]);
            }
            else if (c.Equals("connect"))
            {
                DirectConnect(packet.Address, tokens);
            }
        }

        /*
        ==================
        SV_DirectConnect

        A "connect" OOB command has been received
        ==================
        */
        void DirectConnect(IPEndPoint from, string[] tokens)
        {
            // Check banlist

            string userinfo = tokens[1];
            string schal = Info.ValueForKey(userinfo, "challenge");
            int challenge = int.Parse(schal);
            int qport = int.Parse(Info.ValueForKey(userinfo, "qport"));
            int i;
            // quick reject
            for (i = 0; i < svs.clients.Count; i++)
            {
                client_t cl = svs.clients[i];
                if (cl.state == clientState_t.CS_FREE)
                {
                    continue;
                }
                if (IPAddress.Equals(from.Address, cl.netchan.remoteAddress.Address) && (qport == cl.netchan.qport || from.Port == cl.netchan.remoteAddress.Port))
                {
                    if ((svs.time - cl.lastConnectTime) < 300f)
                    {
                        Common.Instance.WriteLine("DirConnect: ({0})=>Reconnect rejected : too soon.", from.Address.ToString());
                        return;
                    }
                    break;
                }
            }
            string ip = from.Address.ToString();
            Info.SetValueForKey(userinfo, "ip", ip);

            // see if the challenge is valid (LAN clients don't need to challenge)
            if (!IPAddress.IsLoopback(from.Address))
            {
                for (i = 0; i < svs.challenges.Count; i++)
                {
                    if (IPAddress.Equals(from.Address, svs.challenges[i].adr.Address))
                    {
                        if (challenge == svs.challenges[i].challenge)
                            break;
                    }
                }

                if (i == svs.challenges.Count)
                {
                    Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, from, "print\nNo or bad challenge for your address\n");
                    return;
                }

                challenge_t chal = svs.challenges[i];
                if (chal.wasrefused)
                {
                    // Return silently, so that error messages written by the server keep being displayed.
                    return;
                }

                int ping = (int)svs.time - chal.pingTime;

                // never reject a LAN client based on ping
                if (!Net.Instance.IsLanAddress(from.Address))
                {
                    if (ping > 2000)
                    {
                        Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, from, "print\nServer is for low pings only.\n");
                        Common.Instance.WriteLine("Client rejected due to high ping");
                        chal.wasrefused = true;
                        return;
                    }
                }

                Common.Instance.WriteLine("Client {0} connecting with {0} challenge ping", i, ping);
                chal.connected = true;
            }

            bool gotcl = false;
            client_t newCl = new client_t();
            // if there is already a slot for this ip, reuse it
            for (i = 0; i < svs.clients.Count; i++)
            {
                client_t cl = svs.clients[i];
                if (cl.state == clientState_t.CS_FREE)
                {
                    continue;
                }

                if (IPAddress.Equals(from.Address, cl.netchan.remoteAddress.Address) && (qport == cl.netchan.qport || from.Port == cl.netchan.remoteAddress.Port))
                {
                    Common.Instance.WriteLine("{0}:reconnect", from.Address.ToString());
                    newCl = cl;
                    gotcl = true;
                    break;
                }
            }
            if (!gotcl)
            {
                newCl = null;
                // find a client slot
                for (i = 0; i < svs.clients.Count; i++)
                {
                    client_t cl = svs.clients[i];
                    if (cl.state == clientState_t.CS_FREE)
                    {
                        newCl = cl;
                        break;
                    }
                }

                if (newCl == null && i < sv_maxclients.Integer)
                {
                    newCl = new client_t();
                    svs.clients.Add(newCl);
                    i = svs.clients.Count - 1;
                    newCl.id = i;
                }

                if (newCl == null)
                {
                    Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, from, "print\nServer is full\n");
                    Common.Instance.WriteLine("Rejected a connection");
                    return;
                }

                // we got a newcl, so reset the reliableSequence and reliableAcknowledge
                newCl.reliableAcknowledge = 0;
                newCl.reliableSequence = 0;
            }

            // build a new connection
            // accept the new client
            // this is the only place a client_t is ever initialized
            if (sv.gentities.Count <= i)
                sv.gentities.Add(new Common.sharedEntity_t());
            Common.sharedEntity_t ent = sv.gentities[i];
            newCl.gentity = ent;

            // save the challenge
            newCl.challenge = challenge;

            // save the address
            newCl.netchan = Net.Instance.NetChan_Setup(Net.NetSource.SERVER, from, qport);

            // save the userinfo
            newCl.userinfo = userinfo;

            // get the game a chance to reject this connection or modify the userinfo
            string denied = Game.Instance.Client_Connect(i, true);
            if (denied != null)
            {
                Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, from, string.Format("print\n{0}\n", denied));
                Common.Instance.WriteLine("Game rejected a connection: {0}", denied);
                return;
            }

            UserInfoChanged(newCl);

            // send the connect packet to the client
            Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, from, "connectResponse");

            Common.Instance.WriteLine("Going from CS_FREE to CS_CONNECTED for {0}", i);
            newCl.state = clientState_t.CS_CONNECTED;
            newCl.nextSnapshotTime = (int)svs.time;
            newCl.lastPacketTime = svs.time;
            newCl.lastConnectTime = (int)svs.time;

            // when we receive the first packet from the client, we will
            // notice that it is from a different serverid and that the
            // gamestate message was not just sent, forcing a retransmit
            newCl.gamestateMessageNum = -1;

        }

        public void LocateGameData(List<gentity_t> gEnts, List<gclient_t> clients)
        {
            
            sv.gentities = new List<Common.sharedEntity_t>();
            for (int i = 0; i < gEnts.Count; i++)
            {
                sv.gentities.Add(Game.Instance.GEntityToSharedEntity(gEnts[i]));
            }
            sv.num_entities = gEnts.Count;
            sv.gameClients = clients;
        }

        void UserInfoChanged(client_t cl)
        {
            cl.name = Info.ValueForKey(cl.userinfo, "name");

            // rate command
            // if the client is on the same subnet as the server and we aren't running an
            // internet public server, assume they don't need a rate choke
            if (Net.Instance.IsLanAddress(cl.netchan.remoteAddress.Address))
            {
                cl.rate = 99999;
            }
            else
            {
                cl.rate = 20000;
            }

            // snaps command
            string val = Info.ValueForKey(cl.userinfo, "snaps");
            if (val.Length > 0)
            {
                int i = 1;
                int.TryParse(val, out i);
                if (i < 1)
                    i = 1;
                else if (i > sv_fps.Integer)
                    i = sv_fps.Integer;

                cl.snapshotMsec = (int)(1000f / i);
            }
            else
            {
                cl.snapshotMsec = 50;
            }
        }

        /*
        =================
        SV_GetChallenge

        A "getchallenge" OOB command has been received
        Returns a challenge number that can be used
        in a subsequent connectResponse command.
        We do this to prevent denial of service attacks that
        flood the server with invalid connection IPs.  With a
        challenge, they must give a valid IP address.

        If we are authorizing, a challenge request will cause a packet
        to be sent to the authorize server.

        When an authorizeip is returned, a challenge response will be
        sent to that ip.

        ioquake3: we added a possibility for clients to add a challenge
        to their packets, to make it more difficult for malicious servers
        to hi-jack client connections.
        Also, the auth stuff is completely disabled for com_standalone games
        as well as IPv6 connections, since there is no way to use the
        v4-only auth server for these new types of connections.
        =================
        */
        void GetChallenge(IPEndPoint from, string clientChallenge)
        {
            int oldestTime = int.MaxValue;
            int oldest = 0;
            // see if we already have a challenge for this ip
            for (int i = 0; i < svs.challenges.Count; i++)
            {
                challenge_t challenge = svs.challenges[i];
                if (!challenge.connected && IPAddress.Equals(from.Address, challenge.adr.Address))
                {
                    break;
                }

                if (challenge.time < oldestTime)
                {
                    oldestTime = challenge.time;
                    oldest = i;
                }
            }

            challenge_t challeng = new challenge_t();
            // this is the first time this client has asked for a challenge
            if (oldestTime == int.MaxValue)
            {
                
                challeng.clientChallenge = 0;
                challeng.adr = from;
                challeng.firstTime = (int)svs.time;
                challeng.time = (int)svs.time;
                challeng.connected = false;
            }

            // always generate a new challenge number, so the client cannot circumvent sv_maxping
            challeng.challenge = ((new Random().Next() << 16) ^ new Random().Next()) ^ (int)svs.time;
            challeng.wasrefused = false;
            challeng.pingTime = (int)svs.time;
            svs.challenges.Add(challeng);
            Net.Instance.OutOfBandMessage(Net.NetSource.SERVER, challeng.adr, string.Format("challengeResponse {0} {1}", challeng.challenge, clientChallenge));
        }

        public void SpawnServer(string server)
        {
            Common.Instance.WriteLine("------ Server initialization --------");
            Common.Instance.WriteLine("Server: {0}", server);

            // shut down the existing game if it is running
            Game.Instance.Shutdown();

            // if not running a dedicated server CL_MapLoading will connect the client to the server
            // also print some status stuff
            Client.Instance.MapLoading();

            // make sure all the client stuff is unloaded
            Client.Instance.ShutdownAll();

            // clear collision map data
            ClipMap.Instance.ClearMap();

            // init client structures and svs.numSnapshotEntities 
            if (CVars.Instance.VariableValue("sv_running") == 0f)
            {
                Startup();
            }
            else
            {
                if (sv_maxclients.Modified)
                {
                    ChangeMaxClients();
                }
            }

            // FileCache clear pak ref

            // allocate the snapshot entities on the hunk
            svs.snapshotEntities = new List<Common.entityState_t>();
            svs.nextSnapshotEntities = 0;

            // toggle the server bit so clients can detect that a
            // server has changed
            svs.snapFlagServerBit ^= 4;


            foreach (client_t client in svs.clients)
            {
                // save when the server started for each client already connected
                if ((int)client.state >= (int)connstate_t.CONNECTED)
                {
                    client.oldServerTime = sv.time;
                }
            }

            // wipe the entire per-level structure
            ClearServer();
            sv.configstrings = new Dictionary<int, string>();

            CVars.Instance.Set("cl_paused", "0");

            ClipMap.Instance.LoadMap(server, false);

            // set serverinfo visible name
            CVars.Instance.Set("mapname", server);

            // serverid should be different each time
            sv.serverId = (int)Common.Instance.frameTime;
            sv.restartedServerId = sv.serverId; // I suppose the init here is just to be safe
            CVars.Instance.Set("sv_serverid", ""+sv.serverId);

            // clear physics interaction links
            ClearWorld();

            // media configstring setting should be done during
            // the loading stage, so connected clients don't have
            // to load during actual gameplay
            sv.state = serverState_t.SS_LOADING;

            // load and spawn all other entities
            Game.Instance.Init(sv.time, (int)Common.Instance.Milliseconds(), 0);

            // run a few frames to allow everything to settle
            for (int i = 0; i < 3; i++)
            {
                Game.Instance.RunFrame(sv.time);
                sv.time += 100;
                svs.time += 100;
            }

            // create a baseline for more efficient communications
            CreateBaseline();

            for(int i=0; i<svs.clients.Count; i++)
            {
                client_t client = svs.clients[i];
                // send the new gamestate to all connected clients
                if ((int)client.state >= (int)connstate_t.CONNECTED)
                {
                    // connect the client again
                    string denied = Game.Instance.Client_Connect(i, false);
                    if (denied != null)
                    {
                        // this generally shouldn't happen, because the client
                        // was connected before the level change
                        DropClient(client, denied);
                    }
                    else
                    {
                        // when we get the next packet from a connected client,
                        // the new gamestate will be sent
                        client.state = clientState_t.CS_CONNECTED;
                        Common.sharedEntity_t ent = sv.gentities[i];
                        ent.s.number = i;
                        client.gentity = ent;
                        client.deltaMessage = -1;
                        client.nextSnapshotTime = (int)svs.time; // generate a snapshot immediately
                        Game.Instance.Client_Begin(i);
                        //ClientBegin(i);
                        
                    }
                }
            }

            // run another frame to allow things to look at all the players
            Game.Instance.RunFrame(sv.time);
            sv.time += 100;
            svs.time += 100;

            // save systeminfo and serverinfo strings
            string sysInfo = CVars.Instance.InfoString(CVarFlags.SYSTEM_INFO);
            CVars.Instance.modifiedFlags &= ~CVarFlags.SYSTEM_INFO;
            SetConfigString(1, sysInfo);

            SetConfigString(0, CVars.Instance.InfoString(CVarFlags.SERVER_INFO));
            CVars.Instance.modifiedFlags &= ~CVarFlags.SERVER_INFO;

            // any media configstring setting now should issue a warning
            // and any configstring changes should be reliably transmitted
            // to all clients
            sv.state = serverState_t.SS_GAME;

            Common.Instance.WriteLine("---------------------------------------");
        }

        /*
        ================
        SV_CreateBaseline

        Entity baselines are used to compress non-delta messages
        to the clients -- only the fields that differ from the
        baseline will be transmitted
        ================
        */
        void CreateBaseline()
        {
            for (int i = 1; i < sv.svEntities.Length; i++)
            {
                Common.sharedEntity_t svent = sv.gentities[i];
                if (!svent.r.linked)
                    continue;

                svent.s.number = i;

                //
                // take current state as baseline
                //
                sv.svEntities[i].baseline = svent.s;
            }
        }

        private void ClearWorld()
        {
            for (int i = 0; i < sv_worldSectors.Length; i++)
            {
                sv_worldSectors[i] = new worldSector_t();
            }
            sv_numworldSectors = 0;

            Vector3 mins = Vector3.Zero, maxs = Vector3.Zero;
            ClipMap.Instance.ModelBounds(0, ref mins, ref maxs);
            CreateWorldSector(0, mins, maxs);
        }

        private worldSector_t CreateWorldSector(int depth, Vector3 mins, Vector3 maxs)
        {
            worldSector_t anode = new worldSector_t();
            sv_worldSectors[sv_numworldSectors++] = anode;
            if (depth == 4) // AREA_DEPTH = 4
            {
                anode.axis = -1;
                anode.children[0] = anode.children[1] = null;
                return anode;
            }

            Vector3 size = maxs - mins;
            if (size[0] > size[1])
                anode.axis = 0;
            else
                anode.axis = 1;

            anode.dist = 0.5f * (maxs[anode.axis] + mins[anode.axis]);

            Vector3 maxs1 = maxs;
            Vector3 mins2 = mins;

            maxs1[anode.axis] = mins2[anode.axis] = anode.dist;
            anode.children[0] = CreateWorldSector(depth + 1, mins2, maxs);
            anode.children[1] = CreateWorldSector(depth + 1, mins, maxs1);

            return anode;
        }

        private void ClearServer()
        {
            sv.configstrings = new Dictionary<int, string>();
        }

        /*
        ===============
        SV_Startup

        Called when a host starts a map when it wasn't running
        one before.  Successive map or map_restart commands will
        NOT cause this to be called, unless the game is exited to
        the menu system first.
        ===============
        */
        private void Startup()
        {
            if (svs.initialized)
            {
                Common.Instance.WriteLine("Startup: svs.initialized");
                return;
            }

            svs.clients = new List<client_t>();
            svs.numSnapshotEntities = sv_maxclients.Integer * 4 * 64;
            svs.initialized = true;

            CVars.Instance.Set("sv_running", "1");
        }

        private void ChangeMaxClients()
        {
            throw new NotImplementedException();
        }
        // Player movement occurs as a result of packet events, which
        // happen before SV_Frame is called
        internal void Frame(float msec)
        {
            if (Common.Instance.sv_running.Integer == 0)
            {
                // Running as a server, but no map loaded
                return;
            }

            // if it isn't time for the next frame, do nothing
            if (sv_fps.Integer < 1)
                CVars.Instance.Set2("sv_fps", "10", true);

            float frameMsec = 1000f / sv_fps.Integer * Common.Instance.timescale.Value;
            // don't let it scale below 1ms
            if (frameMsec < 1)
            {
                CVars.Instance.Set2("timescale", string.Format("{0}", sv_fps.Integer / 1000f), true);
                frameMsec = 1;
            }

            sv.timeResidual += msec;

            if (sv.restartTime > 0 && sv.time >= sv.restartTime)
            {
                sv.restartTime = 0;
                Commands.Instance.AddText("map_restart 0\n");
                return;
            }

            // update infostrings if anything has been changed
            if ((CVars.Instance.modifiedFlags & CVarFlags.SERVER_INFO) == CVarFlags.SERVER_INFO)
            {
                SetConfigString((int)CVarFlags.SERVER_INFO, CVars.Instance.InfoString(CVarFlags.SERVER_INFO));
                CVars.Instance.modifiedFlags &= ~CVarFlags.SERVER_INFO;
            }
            if ((CVars.Instance.modifiedFlags & CVarFlags.SYSTEM_INFO) == CVarFlags.SYSTEM_INFO)
            {
                SetConfigString((int)CVarFlags.SYSTEM_INFO, CVars.Instance.InfoString(CVarFlags.SYSTEM_INFO));
                CVars.Instance.modifiedFlags &= ~CVarFlags.SYSTEM_INFO;
            }

            CalcPings();

            // run the game simulation in chunks
            while (sv.timeResidual >= frameMsec)
            {
                sv.timeResidual -= frameMsec;
                svs.time += frameMsec;
                sv.time += frameMsec;

                // let everything in the world think and move
                Game.Instance.RunFrame(sv.time);
            }

            // check timeouts
            CheckTimeouts();

            // send messages back to the clients
            SendClientMessages();
        }

        private void SendClientMessages()
        {
            for (int i = 0; i < svs.clients.Count; i++)
            {
                client_t client = svs.clients[i];
                if ((int)client.state <= 0 || client.netchan.connection == null || client.netchan.connection.Status != NetConnectionStatus.Connected)
                {
                    continue;       // not connected
                }

                if (svs.time < client.nextSnapshotTime)
                {
                    continue;   // not time yet
                }

                // generate and send a new message
                SendClientSnapshot(client, i);

            }
        }

        

        /*
        ==================
        SV_CheckTimeouts

        If a packet has not been received from a client for timeout->integer 
        seconds, drop the conneciton.  Server time is used instead of
        realtime to avoid dropping the local client while debugging.

        When a client is normally dropped, the client_t goes into a zombie state
        for a few seconds to make sure any final reliable message gets resent
        if necessary
        ==================
        */
        private void CheckTimeouts()
        {
            int droppoint = (int)(svs.time - 1000 * sv_timeout.Integer);
            int zombiepoint = (int)(svs.time - 1000 * sv_zombietime.Integer);
            for (int i = 0; i < svs.clients.Count; i++)
            {
                client_t cl = svs.clients[i];

                // message times may be wrong across a changelevel
                if (cl.lastPacketTime > svs.time)
                    cl.lastPacketTime = svs.time;

                if (cl.state == clientState_t.CS_ZOMBIE && cl.lastPacketTime < zombiepoint)
                {
                    // using the client id cause the cl->name is empty at this point
                    cl.state = clientState_t.CS_FREE;
                    continue;
                }
                if (cl.state == clientState_t.CS_CONNECTED && cl.lastPacketTime < droppoint)
                {
                    // wait several frames so a debugger session doesn't
                    // cause a timeout
                    if (++cl.timeoutCount > 5)
                    {
                        DropClient(cl, "timed out");
                        cl.state = clientState_t.CS_FREE;
                    }
                }
                else
                {
                    cl.timeoutCount = 0;
                }
            }
        }

        private void DropClient(client_t cl, string reason)
        {
            if (cl.state == clientState_t.CS_ZOMBIE)
                return;     // already dropped

            // see if we already have a challenge for this ip
            challenge_t toRemove = null;
            foreach (challenge_t challenge in svs.challenges)
            {
                if (cl.netchan.remoteAddress.Equals(challenge.adr))
                {
                    toRemove = challenge;
                    //toRemove = challenge;
                    break;
                }
            }
            if (toRemove != null)
                toRemove = new challenge_t();

            // tell everyone why they got dropped
            SendServerCommand(null, string.Format("print \"{0} {1}\"\n", cl.name, reason));

            // call the prog function for removing a client
            // this will remove the body, among other things
            Game.Instance.Client_Disconnect(cl.gentity.s.number);
            //VM_Call(gvm, GAME_CLIENT_DISCONNECT, drop - svs.clients);

            // add the disconnect command
            SendServerCommand(cl, string.Format("disconnect \"{0}\"", reason));

            // nuke user info
            SetUserInfo(cl, "");

            cl.state = clientState_t.CS_ZOMBIE; // become free in a few seconds

        }

        public void UnlinkEntity(Common.sharedEntity_t gEnt)
        {

        }

        public void LinkEntity(Common.sharedEntity_t gEnt)
        {
            svEntity_t ent =  sv.svEntities[gEnt.s.number];
            if (ent.worldSector != null)
            {
                UnlinkEntity(gEnt);
            }

            // encode the size into the entityState_t for client prediction
            if (gEnt.r.bmodel)
            {
                gEnt.s.solid = 0xffffff; // SOLID_BMODEL
            }
            else if ((gEnt.r.contents & (0x2000000 | 1)) > 0)
            {
                // assume that x/y are equal and symetric
                int i = (int)gEnt.r.maxs[0];
                if (i < 1)
                    i = 1;
                if (i > 255)
                    i = 255;

                int j = (int)-gEnt.r.mins[2];
                if (j < 1)
                    j = 1;
                if (j > 255)
                    j = 255;

                int k = (int)gEnt.r.maxs[2] + 32;
                if (k < 1)
                    k = 1;
                if (k > 255)
                    k = 255;

                gEnt.s.solid = (k << 16) | (j << 8) | i;
            }
            else 
            {
                gEnt.s.solid = 0;
            }

            // set the abs box
            if (gEnt.r.bmodel && (gEnt.r.currentAngles[0] != 0 || gEnt.r.currentAngles[1] != 0 || gEnt.r.currentAngles[2] != 0))
            {
                // expand for rotation
                float max = RadiusFromBounds(gEnt.r.mins, gEnt.r.maxs);
                for (int i = 0; i < 3; i++)
                {
                    gEnt.r.absmin[i] = gEnt.r.currentOrigin[i] - max;
                    gEnt.r.absmax[i] = gEnt.r.currentOrigin[i] + max;
                }
            }
            else
            {
                // normal
                gEnt.r.absmin = Vector3.Add(gEnt.r.currentOrigin, gEnt.r.mins);
                gEnt.r.absmax = Vector3.Add(gEnt.r.currentOrigin, gEnt.r.maxs);
            }

            // because movement is clipped an epsilon away from an actual edge,
            // we must fully check even when bounding boxes don't quite touch
            gEnt.r.absmin[0] -= 1;
            gEnt.r.absmin[1] -= 1;
            gEnt.r.absmin[2] -= 1;
            gEnt.r.absmax[0] += 1;
            gEnt.r.absmax[1] += 1;
            gEnt.r.absmax[2] += 1;

            // link to PVS leafs
            ent.numClusters = 0;
            ent.lastCluster = 0;
            ent.areanum = -1;
            ent.areanum2 = -1;

            //get all leafs, including solids
        }

        public static float RadiusFromBounds(Vector3 mins, Vector3 maxs)
        {
            float a, b;
            Vector3 corner = Vector3.Zero;
            for (int i = 0; i < 3; i++)
            {
                a = Math.Abs(mins[i]);
                b = Math.Abs(maxs[i]);
                corner[i] = a > b ? a : b;
            }
            return corner.Length();
        }

        private void SetUserInfo(client_t cl, string p)
        {
            if (cl == null || p == null)
                return;

            foreach (client_t client in svs.clients)
            {
                if (client.Equals(cl))
                {
                    client.userinfo = p;
                    client.name = Info.ValueForKey(p, "name");

                    break;
                }
            }
        }

        public void SendServerCommand(client_t cl, string fmt) 
        {
            // Fix to http://aluigi.altervista.org/adv/q3msgboom-adv.txt
            // The actual cause of the bug is probably further downstream
            // and should maybe be addressed later, but this certainly
            // fixes the problem for now
            if (fmt.Length > 1022)
            {
                Common.Instance.WriteLine("SendServerCommand: too long :(");
                return;
            }

            if (cl != null)
            {
                AddServerCommand(cl, fmt);
                return;
            }

            // send the data to all relevent clients
            foreach (client_t client in svs.clients)
            {
                AddServerCommand(client, fmt);
            }
        }

        private void AddServerCommand(client_t client, string cmd)
        {
            // do not send commands until the gamestate has been sent
            if (client.state < clientState_t.CS_PRIMED)
            {
                return;
            }

            client.reliableSequence++;
            // if we would be losing an old command that hasn't been acknowledged,
            // we must drop the connection
            // we check == instead of >= so a broadcast print added by SV_DropClient()
            // doesn't cause a recursive drop client
            if (client.reliableSequence - client.reliableAcknowledge == 64 + 1)
            {
                Common.Instance.WriteLine("======== pending server commands =========");
                int i;
                for (i = client.reliableAcknowledge+1; i <= client.reliableSequence; i++)
                {
                    Common.Instance.WriteLine("cmd {0}: {1}", i, client.reliableCommands[i & 63]);
                }
                Common.Instance.WriteLine("cmd {0}: {1}", i, cmd);
                DropClient(client, "Server command overflow");
                return;
            }
            int index = client.reliableSequence & 63;
            client.reliableCommands[index] = cmd;
        }

        private void CalcPings()
        {
            for (int i = 0; i < svs.clients.Count; i++)
            {
                client_t cl = svs.clients[i];
                if (cl.state != clientState_t.CS_ACTIVE)
                {
                    cl.ping = 999;
                    continue;
                }
                if (cl.gentity == null)
                {
                    cl.ping = 999;
                    continue;
                }

                int total = 0, count = 0;
                for (int j = 0; j < 32; j++)
                {
                    if (cl.frames[j].messageAcked <= 0)
                        continue;
                    int delta = cl.frames[j].messageAcked - cl.frames[j].messageSent;
                    count++;
                    total += delta;
                }
                if (count <= 0)
                {
                    cl.ping = 999;
                }
                else
                {
                    cl.ping = total / count;
                    if (cl.ping > 999)
                        cl.ping = 999;
                }

                // let the game dll know about the ping
                Common.playerState_t ps = GameClientNum(i);
                ps.ping = cl.ping;
            }
        }

        private Common.playerState_t GameClientNum(int i)
        {
            return sv.gameClients[i].ps;
        }

        

        public void Init()
        {
            sv_fps = CVars.Instance.Get("sv_fps", "30", CVarFlags.TEMP);
            sv_timeout = CVars.Instance.Get("sv_timeout", "200", CVarFlags.TEMP);
            sv_hostname = CVars.Instance.Get("sv_hostname", "noname", CVarFlags.SERVER_INFO | CVarFlags.ARCHIVE);
            sv_mapname = CVars.Instance.Get("mapname", "nomap", CVarFlags.SERVER_INFO | CVarFlags.ARCHIVE);
            CVars.Instance.Get("nextmap", "", CVarFlags.TEMP);
            sv_mapChecksum = CVars.Instance.Get("sv_mapChecksum", "", CVarFlags.ROM);
            sv_maxclients = CVars.Instance.Get("sv_maxclients", "32", CVarFlags.SERVER_INFO | CVarFlags.LATCH);
            sv_zombietime = CVars.Instance.Get("sv_zombietime", "2", CVarFlags.TEMP);
            CVars.Instance.Get("sv_serverid", "0", CVarFlags.SERVER_INFO | CVarFlags.ROM);

            Commands.Instance.AddCommand("map", new CommandDelegate(SV_Map_f));

        }

        void SV_Map_f(string[] tokens)
        {
            if (tokens.Length < 2)
                return;

            // make sure the level exists before trying to change, so that
            // a typo at the server console won't end the game
            string expanded = string.Format("maps/{0}.bsp", tokens[1]);
            if (!FileCache.Instance.Contains(expanded))
            {
                Common.Instance.WriteLine("Can't find map {0}", tokens[1]);
                return;
            }

            SpawnServer(expanded);
        }


        public class server_t {

            public server_t()
            {
                for (int i = 0; i < 1024; i++)
                {
                    svEntities[i] = new svEntity_t();
                }
            }
        	public serverState_t	state;
        	public bool		restarting;			// if true, send configstring changes during SS_LOADING
        	public int				serverId;			// changes each server start
        	public int				restartedServerId;	// serverId before a map_restart
        	public int				checksumFeed;		// the feed key that we use to compute the pure checksum strings
        	// https://zerowing.idsoftware.com/bugzilla/show_bug.cgi?id=475
        	// the serverId associated with the current checksumFeed (always <= serverId)
        	public int       checksumFeedServerId;	
        	public int				snapshotCounter;	// incremented for each snapshot built
        	public float				timeResidual;		// <= 1000 / sv_frame->value
        	public int				nextFrameTime;		// when time > nextFrameTime, process world
        	public Common.cmodel_t[]	models; // 256
        	public Dictionary<int, string>			configstrings = new Dictionary<int, string>(); // 1024
        	public svEntity_t[]		svEntities = new svEntity_t[1024]; // 1024

        	//public char			*entityParsePoint;	// used during game VM init

        	// the game virtual machine will update these on init and changes
        	public List<Common.sharedEntity_t>	gentities;
        	public int				gentitySize;
        	public int				num_entities;		// current number, <= MAX_GENTITIES

        	public List<gclient_t>	gameClients;
        	public int				gameClientSize;		// will be > sizeof(playerState_t) due to game private data

        	public int				restartTime;
        	public float				time;
        } 

            public enum serverState_t {
            	SS_DEAD,			// no map loaded
            	SS_LOADING,			// spawning level entities
            	SS_GAME				// actively running
            } 

        public class worldSector_t {
        	public int		axis;		// -1 = leaf node
        	public float	dist;
        	public worldSector_t[]	children = new worldSector_t[2]; // 2
        	public List<svEntity_t>	entities = new List<svEntity_t>();
        } 

            public class svEntity_t {
            	public worldSector_t worldSector;
            	public svEntity_t nextEntityInWorldSector;
            	
            	public Common.entityState_t	baseline;		// for delta compression of initial sighting
            	public int			numClusters;		// if -1, use headnode instead
            	public int[]			clusternums; // 16
            	public int			lastCluster;		// if all the clusters don't fit in clusternums
            	public int			areanum, areanum2;
            	public int			snapshotCounter;	// used to prevent double adding from portal views
            }

        // this structure will be cleared only when the game dll changes
            public struct serverStatic_t {
            	public bool	initialized;				// sv_init has completed

                public float time;						// will be strictly increasing across level changes

                public int snapFlagServerBit;			// ^= SNAPFLAG_SERVERCOUNT every SV_SpawnServer()

                public List<client_t> clients;					// [sv_maxclients->integer];
                public int numSnapshotEntities;		// sv_maxclients->integer*PACKET_BACKUP*MAX_PACKET_ENTITIES
                public int nextSnapshotEntities;		// next snapshotEntities to use
                public List<Common.entityState_t> snapshotEntities;		// [numSnapshotEntities]
                public int nextHeartbeatTime;
                public List<challenge_t> challenges;	// 1024 to prevent invalid IPs from connecting
                //netadr_t	redirectAddress;			// for rcon return messages

                //netadr_t	authorizeAddress;			// for rcon return messages
            }

        public class client_t {
            public client_t()
            {
                for (int i = 0; i < 32; i++)
                {
                    frames[i] = new clientSnapshot_t();
                }
            }
            public clientState_t state;
            public string userinfo;		// 1024 name, etc

            public string[] reliableCommands; // 64 - 1024 max len
            public int reliableSequence;		// last added reliable message, not necesarily sent or acknowledged yet
            public int reliableAcknowledge;	// last acknowledged reliable message
            public int reliableSent;			// last sent reliable message, not necesarily acknowledged yet
            public int messageAcknowledge;

            public int gamestateMessageNum;	// netchan->outgoingSequence of gamestate
            public int challenge;

            public Input.UserCommand lastUsercmd;
            public int lastMessageNum;		// for delta compression
            public int lastClientCommand;	// reliable client message sequence
            public string lastClientCommandString = ""; // 1024
            public Common.sharedEntity_t gentity;			// SV_GentityNum(clientnum)
            public string name;			// 32 extracted from userinfo, high bits masked

            public int deltaMessage;		// frame last client usercmd message
            public int nextReliableTime;	// svs.time when another reliable command will be allowed
            public float lastPacketTime;		// svs.time when packet was last received
            public int lastConnectTime;	// svs.time when connection started
            public int nextSnapshotTime;	// send another snapshot when svs.time >= nextSnapshotTime
            public bool rateDelayed;		// true if nextSnapshotTime was set based on rate instead of snapshotMsec
            public int timeoutCount;		// must timeout a few frames in a row so debugging doesn't break
            public clientSnapshot_t[] frames = new clientSnapshot_t[32];	// 32 updates can be delta'd from here
            public int ping;
            public int rate;				// bytes / second
            public int snapshotMsec;		// requests a snapshot every snapshotMsec unless rate choked
            public int pureAuthentic;
            public bool gotCP; // TTimo - additional flag to distinguish between a bad pure checksum, and no cp command at all
            public Net.netchan_t netchan;
        	// TTimo
        	// queuing outgoing fragmented messages to send them properly, without udp packet bursts
        	// in case large fragmented messages are stacking up
        	// buffer them into this queue, and hand them out to netchan as needed
            //netchan_buffer_t *netchan_start_queue;
            //netchan_buffer_t **netchan_end_queue;


            public float oldServerTime;
            public bool[] csUpdated = new bool[1025];	 // 1025
            public int id;
        }

        public class clientSnapshot_t {
        	public int				areabytes;
            public byte[] areabits = new byte[32];		// 32 portalarea visibility bits
            public Common.playerState_t ps = new Common.playerState_t();
            public int num_entities;
            public int first_entity;		// into the circular sv_packet_entities[]
        										// the entities MUST be in increasing state number
        										// order, otherwise the delta compression will fail
            public int messageSent;		// time the message was transmitted
            public int messageAcked;		// time the message was acked
            public int messageSize;		// used to rate drop packets
        }

        public enum clientState_t
        {
        	CS_FREE,		// can be reused for a new connection
        	CS_ZOMBIE,		// client has been disconnected, but don't reuse
        					// connection for a couple seconds
        	CS_CONNECTED,	// has been assigned to a client_t, but no gamestate yet
        	CS_PRIMED,		// gamestate has been sent, but client hasn't sent a usercmd
        	CS_ACTIVE		// client is fully in game
        }

        public class challenge_t
        {
        	public IPEndPoint	adr;
            public int challenge;
            public int clientChallenge;		// challenge number coming from the client
            public int time;				// time the last packet was sent to the autherize server
            public int pingTime;			// time the challenge response was sent to client
            public int firstTime;			// time the adr was first used, for authorize timeout checks
            public bool wasrefused;
            public bool connected;
        } 
    }
}
