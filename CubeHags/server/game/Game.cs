using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using SlimDX;
using CubeHags.client;
using CubeHags.client.cgame;

namespace CubeHags.server
{
    public delegate void ThinkDelegate(gentity_t ent);
    public sealed partial class Game
    {
        private static readonly Game _Instance = new Game();
        public static Game Instance {get {return _Instance;}}

        public level_locals_t level;
        public List<gentity_t> g_entities;
        public List<gclient_t> g_clients;

        public Game()
        {
            spawns  = new spawn_t[] { new spawn_t{Name = "info_player_start", Spawn = new SpawnDelegate(SP_info_player_start)} };
        }

        public Server.svEntity_t SvEntityForGentity(Common.sharedEntity_t gEnt) 
        {
            if (gEnt == null || gEnt.s.number < 0 || gEnt.s.number >= 1024)
                Common.Instance.Error("SvEntityForGentity: Bad gEnt");
            return Server.Instance.sv.svEntities[gEnt.s.number];
        }

        // Advances the non-player objects in the world
        public void RunFrame(float levelTime)
        {
            // if we are waiting for the level to restart, do nothing
            if (level.restarted)
                return;

            level.framenum++;
            level.previousTime = level.time;
            level.time = levelTime;
            float msec = level.time - level.previousTime;

            // UpdateCvars
            //UpdateCvars();

            //
            // go through all allocated objects
            //
            float start = Common.Instance.Milliseconds();
            gentity_t ent;
            for (int i = 0; i < level.num_entities; i++)
            {
                ent = g_entities[i];
                if (!ent.inuse)
                    continue;

                // clear events that are too old
                if (level.time - ent.eventTime > 300)
                {
                    
                    // Run event
                    // FIX

                    if (ent.freeAfterEvent)
                    {
                        FreeEntity(ent);
                        continue;
                    }
                    else if (ent.unlinkAfterEvent)
                    {
                        ent.unlinkAfterEvent = false;
                        // TODO: Unlink event
                    }
                }

                if (ent.freeAfterEvent)
                    continue;

                if (!ent.r.linked && ent.neverFree)
                {
                    continue;
                }

                if (ent.s.eType == 3)// Missile
                {
                    //RunMissile(ent);
                    continue;
                }

                // ITEM
                if (ent.s.eType == 2 || ent.physicsObject)
                {
                    //RunItem(ent);
                    continue;
                }

                if (ent.s.eType == 4)
                {
                    // RunMover(ent);
                    continue;
                }

                if (i < 64)
                {
                    RunClient(ent);
                    continue;
                }

                RunThink(ent);
            }

            float end = Common.Instance.Milliseconds();

            start = Common.Instance.Milliseconds();
            for (int i = 0; i < level.maxclients; i++)
            {
                ent = g_entities[i];
                if (ent.inuse)
                    ClientEndFrame(ent);
            }
            end = Common.Instance.Milliseconds();


        }

        /*
        ==============
        ClientEndFrame

        Called at the end of each server frame for each connected client
        A fast client will have multiple ClientThink for each ClientEdFrame,
        while a slow client may have multiple ClientEndFrame between ClientThink.
        ==============
        */
        private void ClientEndFrame(gentity_t ent)
        {
            if (ent.client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
            {
                SpectatorClientEndFrame(ent);
                return;
            }

            clientPersistant_t pers = ent.client.pers;

            if (level.intermissiontime > 0)
                return;

            // set the latest infor
            CGame.PlayerStateToEntityState(ent.client.ps, ent.s, true);
            SendPredictableEvents(ent.client.ps);
        }

        private void SendPredictableEvents(Common.playerState_t ps)
        {
            // if there are still events pending
            if (ps.entityEventSequence < ps.eventSequence)
            {
                // create a temporary entity for this event which is sent to everyone
                // except the client who generated the event
                int seq = ps.entityEventSequence & 1;
                int evt = ps.events[seq] | ((ps.entityEventSequence & 3) << 8);
                // set external event to zero before calling BG_PlayerStateToEntityState
                int extEvent = ps.externalEvent;
                ps.externalEvent = 0;
                // create temporary entity for event
                gentity_t t = TempEntity(ps.origin, evt);
                int number = t.s.number;
                CGame.PlayerStateToEntityState(ps, t.s, true);
                t.s.number = number;
                t.s.eType = 13 + evt;
                t.s.eFlags |= 0x00000010;
                t.s.otherEntityNum = ps.clientNum;
                // send to everyone except the client who generated the event
                t.r.svFlags |= Common.svFlags.NOTSINGLECLIENT;
                t.r.singleClient = ps.clientNum;
                // set back external event
                ps.externalEvent = extEvent;
            }
        }


        /*
        =================
        G_TempEntity

        Spawns an event entity that will be auto-removed
        The origin will be snapped to save net bandwidth, so care
        must be taken if the origin is right on a surface (snap towards start vector first)
        =================
        */
        private gentity_t TempEntity(Vector3 origin, int evt)
        {
            gentity_t e = Spawn();
            e.s.eType = 13 + evt;
            e.classname = "tempEntity";
            e.eventTime = (int)level.time;
            e.freeAfterEvent = true;
            Vector3 snapped = origin;
            snapped = CGame.SnapVector(snapped);
            SetOrigin(e, snapped);

            Server.Instance.LinkEntity(GEntityToSharedEntity(e));

            return e;
        }


        /*
        =================
        G_Spawn

        Either finds a free entity, or allocates a new one.

          The slots from 0 to MAX_CLIENTS-1 are always reserved for clients, and will
        never be used by anything else.

        Try to avoid reusing an entity that was recently freed, because it
        can cause the client to think the entity morphed into something else
        instead of being removed and recreated, which can cause interpolated
        angles and bad trails.
        =================
        */
        private gentity_t Spawn()
        {
            int i = 0;
            gentity_t e = null;
            for (int force = 0; force < 2; force++)
            {
                // if we go through all entities and can't find one to free,
                // override the normal minimum times before use
                int maxclients = 64;
                e = g_entities[maxclients];
                for (i = maxclients; i < level.num_entities; i++)
                {
                    e = g_entities[i];
                    if (e.inuse)
                        continue;

                    // the first couple seconds of server time can involve a lot of
                    // freeing and allocating, so relax the replacement policy
                    if (force == 0 && e.freetime > level.startTime + 2000 && level.time - e.freetime < 1000)
                    {
                        continue;
                    }

                    // reuse this slot
                    InitGentity(e, i);
                    return e;
                }
                if (i != 1024)
                    break;
            }
            if (i == 1024 - 2)
            {
                for (i = 0; i < 1024; i++)
                {
                    Common.Instance.WriteLine("{0}: {1}", i, g_entities[i].classname);
                }
                Common.Instance.Error("Spawn: No free entities");
            }

            // open up a new slot
            level.num_entities++;

            // let the server system know that there are more entities
            Server.Instance.LocateGameData(level.gentities, level.clients);
            e = g_entities[level.num_entities - 1];
            InitGentity(e, level.num_entities-1);
            return e;
        }

        private void InitGentity(gentity_t e, int index)
        {
            e.inuse = true;
            e.classname = "noclass";
            e.s.number = index;
            e.r.ownerNum = 1023;
        }

        private void SpectatorClientEndFrame(gentity_t ent)
        {
            // if we are doing a chase cam or a remote view, grab the latest info
            if (ent.client.sess.spectatorState == spectatorState_t.SPECTATOR_FOLLOW)
            {
                int clientNum = ent.client.sess.spectatorClient;
                // team follow1 and team follow2 go to whatever clients are playing
                if (clientNum == -1)
                    clientNum = level.follow1;
                else if (clientNum == -2)
                    clientNum = level.follow2;

                if (clientNum >= 0)
                {
                    gclient_t cl = level.clients[clientNum];
                    if (cl.pers.connected == clientConnected_t.CON_CONNECTED && cl.sess.sessionTeam != team_t.TEAM_SPECTATOR)
                    {
                        //int flags = (cl.ps.eFlags & ~())
                        ent.client.ps = cl.ps;
                        ent.client.ps.pm_flags |= PMFlags.FOLLOW;
                        ent.client.ps.eFlags = cl.ps.eFlags; // FIX
                        return;
                    }
                    else
                    {
                        // drop them to free spectators unless they are dedicated camera followers
                        if (ent.client.sess.spectatorClient >= 0)
                        {
                            ent.client.sess.spectatorState = spectatorState_t.SPECTATOR_FREE;
                            Client_Begin(ent.client.ps.clientNum);
                        }
                    }
                }
            }

            if (ent.client.sess.spectatorState == spectatorState_t.SPECTATOR_SCOREBOARD)
                ent.client.ps.pm_flags |= PMFlags.SCOREBOARD;
            else
                ent.client.ps.pm_flags &= ~PMFlags.SCOREBOARD;
        }

        private void RunThink(gentity_t ent)
        {
            float thinktime = ent.nextthink;
            if (thinktime <= 0)
                return;

            if (thinktime > level.time)
                return;

            ent.nextthink = 0;
            
            ent.RunThink(ent);
        }

        
        private void RunClient(gentity_t ent)
        {
            //ent.client.pers.cmd.serverTime = (int)level.time;
            Client_Think(ent);
        }

        private void FreeEntity(gentity_t ent)
        {
            // TODO: UnlinkFromWorld
            if (ent.neverFree)
                return;

            // TODO: CLear entity
            ent.classname = "freed";
            ent.freetime = level.time;
            ent.inuse = false;
            //ent = new gentity_t();
        }

        /*
        ===========
        ClientConnect

        Called when a player begins connecting to the server.
        Called again for every map change or tournement restart.

        The session information will be valid after exit.

        Return NULL if the client should be allowed, otherwise return
        a string with the reason for denial.

        Otherwise, the client will be sent the current gamestate
        and will eventually get to ClientBegin.

        firstTime will be qtrue the very first time a client connects
        to the server machine, but qfalse on map changes and tournement
        restarts.
        ============
        */
        public string Client_Connect(int clientNum, bool firstTime)
        {
            gentity_t ent = g_entities[clientNum];
            string userinfo = Server.Instance.svs.clients[clientNum].userinfo;

            // they can connect
            level.clients[clientNum] = new gclient_t();
            ent.client = level.clients[clientNum];
            gclient_t client = ent.client;
            client.pers.connected = clientConnected_t.CON_CONNECTING;
            // read or initialize the session data
            if (firstTime || level.newSession)
            {
                InitSessionData(client, clientNum, userinfo);
            }
            ReadSessionData(client, clientNum);

            // get and distribute relevent paramters
            Common.Instance.WriteLine("ClientConnect: {0}", clientNum);
            ClientUserInfoChanged(clientNum);

            // don't do the "xxx connected" messages if they were caried over from previous level
            if (firstTime)
            {
                Server.Instance.SendServerCommand(null, string.Format("print \"{0} connected\"\n", client.pers.netname));
            }

            return null;
        }


        /*
        ===========
        ClientUserInfoChanged

        Called from ClientConnect when the player first connects and
        directly by the server system when the player updates a userinfo variable.

        The game can override any of the settings and call trap_SetUserinfo
        if desired.
        ============
        */
        public void ClientUserInfoChanged(int clientNum)
        {
            gclient_t client = g_entities[clientNum].client;
            string info = GetUserInfo(clientNum);

            if (!Info.Validate(info))
            {
                info = "\\name\\badinfo";
            }

            // check for local client
            string s = Info.ValueForKey(info, "ip");
            if (s.Equals("localhost"))
                client.pers.localClient = true;

            // set name
            string oldname = client.pers.netname;
            s = Info.ValueForKey(info, "name");
            client.pers.netname = ClientCleanName(s);

            if (client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
            {
                if (client.sess.spectatorState == spectatorState_t.SPECTATOR_SCOREBOARD)
                    client.pers.netname = "scoreboard";
            }

            if (client.pers.connected == clientConnected_t.CON_CONNECTED)
            {
                if (!client.pers.netname.Equals(oldname))
                {
                    Server.Instance.SendServerCommand(null, string.Format("print \"{0} renamed to {1}\n\"", oldname, client.pers.netname));
                }
            }

            string model = Info.ValueForKey(info, "model");

            client.pers.maxHealth = 100;
            client.ps.stats[6] = client.pers.maxHealth;

            s = string.Format("n\\{0}\\t\\{1}\\hmodel\\{2}\\hc\\{3}", client.pers.netname, (int)client.sess.sessionTeam, model, client.pers.maxHealth);

            Server.Instance.SetConfigString((int)ConfigString.CS_PLAYERS + clientNum, s);

            // this is not the userinfo, more like the configstring actually
            Common.Instance.WriteLine("ClientUserInfoChanged: {0} {1}", clientNum, s);
        }

        string ClientCleanName(string s)
        {
            string res = s.Trim();
            if (res.Length == 0)
                res = "UnnamedPlayer";

            return res;
        }

        public void Shutdown()
        {

        }

        /*
        ===========
        ClientDisconnect

        Called when a player drops from the server.
        Will not be called between levels.

        This should NOT be called directly by any game logic,
        call trap_DropClient(), which will call this and do
        server system housekeeping.
        ==========
        */
        public void Client_Disconnect(int clientNum)
        {
            gentity_t ent = g_entities[clientNum];
            if (ent.client == null)
                return;

            // stop any following clients
            for (int i = 0; i < level.maxclients; i++)
            {
                if (level.clients[i].sess.sessionTeam == team_t.TEAM_SPECTATOR &&
                    level.clients[i].sess.spectatorState == spectatorState_t.SPECTATOR_FOLLOW &&
                    level.clients[i].sess.spectatorClient == clientNum)
                {
                    StopFollowing(g_entities[i]);
                }
            }


            ent.s.modelindex = 0;
            ent.inuse = false;
            ent.classname = "disconnected";
            ent.client.pers.connected = clientConnected_t.CON_DISCONNECTED;
            ent.client.ps.persistant[(int)persEnum_t.PERS_TEAM] = (int)team_t.TEAM_FREE;
            ent.client.sess.sessionTeam = team_t.TEAM_FREE;
        }

        private void StopFollowing(gentity_t gentity_t)
        {
            throw new NotImplementedException();
        }

        public void Client_Command(int clientNum, string[] tokens)
        {
            gentity_t ent = g_entities[clientNum];
            if (ent.client == null)
                return; // not fully in game yet
            // FIX IMPLEMENT

        }

        public void Client_Think(int clientNum)
        {
            gentity_t ent = g_entities[clientNum];
            Client_Think(ent);
        }
        /*
        ==================
        ClientThink

        A new command has arrived from the client
        This will be called once for each client frame, which will
        usually be a couple times for each server frame on fast clients.
        ==================
        */
        public void Client_Think(gentity_t ent)
        {
            ent.client.pers.cmd = Server.Instance.GetUsercmd(ent.s.clientNum);
            // mark the time we got info, so we can display the
            // phone jack if they don't get any for a while
            ent.client.lastCmdTime = (int)level.time;

            // don't think if the client is not yet connected (and thus not yet spawned in)
            if (ent.client.pers.connected != clientConnected_t.CON_CONNECTED)
                return;

            Input.UserCommand ucmd = ent.client.pers.cmd;

            int msec = ucmd.serverTime - ent.client.ps.commandTime;
            // following others may result in bad times, but we still want
            // to check for follow toggles
            if (msec < 1 && ent.client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
                return;
            if (msec > 200)
                msec = 200;

            // spectators don't do much
            if (ent.client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
            {
                SpectatorThink(ent, ucmd);
                return;
            }


        }

        void SpectatorThink(gentity_t ent, Input.UserCommand ucmd)
        {
            gclient_t client = ent.client;
            if (client.sess.spectatorState != spectatorState_t.SPECTATOR_FOLLOW)
            {
                client.ps.pm_type = Common.PMType.SPECTATOR;
                client.ps.speed = 400;  // faster than normal
                
                // set up for pmove
                pmove_t pm = new pmove_t();
                pm.ps = client.ps;
                pm.cmd = ucmd;
                pm.tracemask = 1;
                pm.mins = new Vector3(-15, -15, -24);
                pm.maxs = new Vector3(15, 15, 32);
                //pm.Trace += new TraceDelegate(Server.Instance.Trace);
                //pm.PointContents += new TraceContentsDelegate(Server.Instance.PointContents);

                // perform a pmove
                Common.Instance.Pmove(pm);
                //if(!pm.ps.velocity.Equals(Vector3.Zero))
                //Common.Instance.WriteLine("vel: {0}", pm.ps.velocity);
                // save results of pmove
                ent.s.origin = client.ps.origin;

                Server.Instance.UnlinkEntity(GEntityToSharedEntity(ent));
            }
            client.oldbuttons = client.buttons;
            client.buttons = ucmd.buttons;
        }

        /*
        ===========
        ClientBegin

        called when a client has finished connecting, and is ready
        to be placed into the level.  This will happen every level load,
        and on transition between teams, but doesn't happen on respawns
        ============
        */
        public void Client_Begin(int clientNum)
        {
            gentity_t ent = g_entities[clientNum];
            gclient_t client = level.clients[clientNum];

            if (ent.r.linked)
            {
                Server.Instance.UnlinkEntity(GEntityToSharedEntity(ent));
            }
            InitEntity(ent);
            ent.client = client;
            client.pers.connected = clientConnected_t.CON_CONNECTED;
            client.pers.enterTime = (int)level.time;
            client.pers.teamState.state = playerTeamStateState_t.TEAM_BEGIN;

            // save eflags around this, because changing teams will
            // cause this to happen with a valid entity, and we
            // want to make sure the teleport bit is set right
            // so the viewpoint doesn't interpolate through the
            // world to the new position
            int flags = client.ps.eFlags;
            client.ps = new Common.playerState_t();
            client.ps.eFlags = flags;

            // locate ent at a spawn point
            ClientSpawn(ent);

            Server.Instance.LocateGameData(level.gentities, level.clients);

            //if (client.sess.sessionTeam != 3)
            //{
            //    // send event
            //    gentity_t tent = TempEntity(ent.client.ps.origin, 42); // EV_PLAYER_TELEPORT_IN
            //    tent.s.clientNum = ent.s.clientNum;

            //    SendServerCommand(-1, string.Format("print \"{0} entered the game\n\"", client.pers.netname));
            //}

            Common.Instance.WriteLine("Client_Begin {0}", clientNum);
        }


        /*
        ===========
        ClientSpawn

        Called every time a client is placed fresh in the world:
        after the first ClientBegin, and after each respawn
        Initializes all non-persistant parts of playerState
        ============
        */
        static Vector3 playerMins = new Vector3( -15, -15, -24 );
        static Vector3 playerMaxs = new Vector3(15, 15, 32);
        void ClientSpawn(gentity_t ent)
        {
            int index = ent.s.clientNum;
            gclient_t client = ent.client;

            Vector3 spawn_origin = Vector3.Zero;
            Vector3 spawn_angles = Vector3.Zero;
            spawn_origin = new Vector3(0, 100, -100);

            gentity_t spawnpoint;
            // find a spawn point
            // do it before setting health back up, so farthest
            // ranging doesn't count this client
            if (client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
            {
                spawnpoint = SelectSpectatorSpawnPoint(ref spawn_origin, ref spawn_angles);
            }
            else
            {
                // the first spawn should be at a good looking spot
                if (!client.pers.initialSpawn && client.pers.localClient)
                {
                    client.pers.initialSpawn = true;
                    spawnpoint = SelectInitialSpawnPoint(ref spawn_origin, ref spawn_angles);
                }
                else
                {
                    // don't spawn near existing origin if possible
                    spawnpoint = SelectRandomFurthestSpawnPoint(client.ps.origin, ref spawn_origin, ref spawn_angles);
                }
            }

            client.pers.teamState.state = playerTeamStateState_t.TEAM_ACTIVE;

            // toggle the teleport bit so the client knows to not lerp
            // and never clear the voted flag
            int flags = ent.client.ps.eFlags & 0x00000004;
            flags ^= 0x00000004;

            // clear everything but the persistant data
            clientPersistant_t saved = client.pers;
            clientSession_t savedSess = client.sess;
            int savedPing = client.ps.ping;
            int accuracyhits = client.accuracy_hits;
            int accuracyshots = client.accuracy_shots;
            int[] persistant = new int[16];
            for (int i = 0; i < 16; i++)
            {
                persistant[i] = client.ps.persistant[i];
            }
            int eventSequence = client.ps.eventSequence;
            //client = new gclient_t();
            //ent.client = client;
            client.pers = saved;
            client.sess = savedSess;
            client.ps.ping = savedPing;
            client.accuracy_hits = accuracyhits;
            client.accuracy_shots = accuracyshots;
            client.lastkilled_client = -1;
            for (int i = 0; i < 16; i++)
            {
                client.ps.persistant[i] = persistant[i];
            }
            client.ps.eventSequence = eventSequence;
            client.ps.persistant[4]++;
            client.ps.persistant[3] = (int)client.sess.sessionTeam;

            client.airOutTime = (int)level.time + 12000;

            string userinfo = GetUserInfo(index);
            // set max health
            client.pers.maxHealth = 100;
            // clear entity values
            client.ps.stats[6] = client.pers.maxHealth;
            client.ps.eFlags = flags;

            ent.s.groundEntityNum = 1023; // none?
            //ent.client = level.clients[index];
            ent.takedamage = true;
            ent.inuse = true;
            ent.classname = "player";
            ent.r.contents = 0x2000000;
            ent.clipmask = 0x2000000;
            ent.waterlevel = 0;
            ent.flags = 0;
            ent.watertype = 0;
            ent.r.mins = playerMins;
            ent.r.maxs = playerMaxs;

            client.ps.clientNum = index;
            client.ps.stats[2] = 1 << 2;

            // health will count down towards max_health
            ent.health = client.ps.stats[0] = client.ps.stats[6] + 25;

            SetOrigin(ent, spawn_origin);
            client.ps.origin = spawn_origin;

            // the respawned flag will be cleared after the attack and jump keys come up
            client.ps.pm_flags |= PMFlags.RESPAWNED; // Respawned

            ent.client.pers.cmd =  GetUserCommand(index);
            SetClientViewAngle(ent, spawn_angles);

            if (ent.client.sess.sessionTeam != team_t.TEAM_SPECTATOR)
            {
                KillBox(ent);
                Server.Instance.LinkEntity(GEntityToSharedEntity(ent));
            }

            // don't allow full run speed for a bit
            client.ps.pm_flags |= PMFlags.TIME_KNOCKBACK;
            client.ps.pm_time = 100;

            client.respawnTime = (int)level.time;
            client.inactivityTime = (int)level.time + 10000;
            client.latched_buttons = 0;

            if (level.intermissiontime == 1)
            {
                //MoveClientToIntermission(ent);
            }
            else
            {
                // fire the targets of the spawn point
                UseTargets(spawnpoint, ent);
            }

            // run a client frame to drop exactly to the floor,
            // initialize animations and other things
            client.ps.commandTime = (int)level.time - 100;
            ent.client.pers.cmd.serverTime = (int)level.time;
            Client_Think(ent);

            // positively link the client, even if the command times are weird
            if (ent.client.sess.sessionTeam != team_t.TEAM_SPECTATOR)
            {
                CGame.PlayerStateToEntityState(client.ps, ent.s, true);
                ent.r.currentOrigin = ent.client.ps.origin;
                Server.Instance.LinkEntity(GEntityToSharedEntity(ent));
            }
            

            // run the presend to set anything else
            ClientEndFrame(ent);

            // clear entity state values
            CGame.PlayerStateToEntityState(client.ps, ent.s, true);
        }

        /*
        ==============================
        G_UseTargets

        "activator" should be set to the entity that initiated the firing.

        Search for (string)targetname in all entities that
        match (string)self.target and call their .use function

        ==============================
        */
        void UseTargets(gentity_t ent, gentity_t activator)
        {
            if (ent == null)
                return;

            if (ent.target == null)
                return;

            gentity_t t = null;
            int tc = 0;
            while ((t = Find(tc++, "targetname", ent.target)) != null)
            {
                if (t == ent)
                {
                    Common.Instance.WriteLine("WARNING: Entity used itself. :O");
                }
                else
                {
                    if (t.use != null)
                    {
                        t.use(t, ent, activator);
                    }
                }
                if (!ent.inuse)
                {
                    Common.Instance.WriteLine("entity was removed while using targets");
                    return;
                }
            }
        }

        /*
        =================
        G_KillBox

        Kills all entities that would touch the proposed new positioning
        of ent.  Ent should be unlinked before calling this!
        =================
        */
        void KillBox(gentity_t ent)
        {
            Vector3 mins, maxs;
            mins = Vector3.Add(ent.client.ps.origin, ent.r.mins);
            maxs = Vector3.Add(ent.client.ps.origin, ent.r.maxs);
            int[] touch = new int[1024];
            int num = 0;//EntitiesInBox(mins, maxs, ref touch);

            gentity_t hit;
            for (int i = 0; i < num; i++)
            {
                hit = g_entities[touch[i]];
                if (hit.client == null)
                    continue;

                // nail it
                //Damage(hit, ent, ent, null, null, 100000);
            }
        }

        void SetClientViewAngle(gentity_t ent, Vector3 angle)
        {
            // set the delta angle
            int cmdAngle = (((int)angle[0] * 65535 / 360) & 65535);
            ent.client.ps.delta_angles[0] = cmdAngle - ent.client.pers.cmd.anglex;
            cmdAngle = (((int)angle[1] * 65535 / 360) & 65535);
            ent.client.ps.delta_angles[1] = cmdAngle - ent.client.pers.cmd.angley;
            cmdAngle = (((int)angle[2] * 65535 / 360) & 65535);
            ent.client.ps.delta_angles[2] = cmdAngle - ent.client.pers.cmd.anglez;
            ent.s.angles = angle;
            ent.client.ps.viewangles = ent.s.angles;
        }

        Input.UserCommand GetUserCommand(int index)
        {
            if (index < 0 || index >= Server.Instance.svs.clients.Count)
            {
                Common.Instance.Error(string.Format("GetUserCommand: bad clientNum: {0}", index));
            }
            return Server.Instance.svs.clients[index].lastUsercmd;
        }

        void SetOrigin(gentity_t ent, Vector3 origin)
        {
            ent.s.pos.trBase = origin;
            ent.s.pos.trType = Common.trType_t.TR_STATIONARY;
            ent.s.pos.trTime = 0;
            ent.s.pos.trDuration = 0;
            ent.s.pos.trDelta = Vector3.Zero;
            ent.r.currentOrigin = origin;
        }

        string GetUserInfo(int index)
        {
            return Server.Instance.svs.clients[index].userinfo;
        }

        void InitEntity(gentity_t ent)
        {
            ent.inuse = true;
            ent.classname = "noclass";
            ent.s.number = g_entities.Count;
            ent.r.ownerNum = 1023; // None?
        }

        public Common.sharedEntity_t GEntityToSharedEntity(gentity_t ent)
        {
            Common.sharedEntity_t sent = new Common.sharedEntity_t();
            sent.s = ent.s;
            sent.r = ent.r;
            return sent;
        }

        public void Init(float levelTime, int randSeed, int restart)
        {
            level.time = levelTime;
            level.startTime = levelTime;
            level.logFile = new System.IO.StreamWriter("gamelog.txt");
            level.newSession = true;

            // initialize all entities for this game
            g_entities = new List<gentity_t>();
            for (int i = 0; i < 1024; i++)
			{
                g_entities.Add(new gentity_t());
			}
            level.gentities = g_entities;

            // initialize all clients for this game
            level.maxclients = 32;
            g_clients = new List<gclient_t>();
            for (int i = 0; i < level.maxclients; i++)
            {
                g_clients.Add(new gclient_t());
            }
            level.clients = g_clients;

            // set client fields on player ents
            for (int i = 0; i < level.maxclients; i++)
            {
                gentity_t ent =  g_entities[i];
                ent.client = level.clients[i];
            }

            // always leave room for the max number of clients,
            // even if they aren't all used, so numbers inside that
            // range are NEVER anything but clients
            level.num_entities = 64;
            
            Server.Instance.LocateGameData(level.gentities, level.clients);

            SpawnEntitiesFromString();

            FindTeams();
        }

        void FindTeams() 
        {
            int c = 0, c2 = 0;
            for (int i = 0; i < level.num_entities; i++)
            {
                gentity_t e = level.gentities[i];
                if (!e.inuse)
                    continue;
                if (e.team == null)
                    continue;
                if ((e.flags & gentityFlags.FL_TEAMSLAVE) == gentityFlags.FL_TEAMSLAVE)
                    continue;
                e.teammaster = e;
                c++;
                c2++;
                for (int j = i+1; j < level.num_entities; j++)
                {
                    gentity_t e2 = level.gentities[j];
                    if (!e2.inuse)
                        continue;
                    if (e2.team == null)
                        continue;
                    if ((e2.flags & gentityFlags.FL_TEAMSLAVE) == gentityFlags.FL_TEAMSLAVE)
                        continue;
                    if (e.team.Equals(e2.team))
                    {
                        c2++;
                        e2.teamchain = e.teamchain;
                        e.teamchain = e2;
                        e2.teammaster = e;
                        e2.flags |= gentityFlags.FL_TEAMSLAVE;

                        // make sure targets only point at the master
                        if (e2.targetname != null)
                        {
                            e.targetname = e2.targetname;
                            e2.targetname = null;
                        }
                    }
                }
            }
        }

        // ConsoleCommand will be called when a command has been issued
        // that is not recognized as a builtin function.
        // The game can issue trap_argc() / trap_argv() commands to get the command
        // and parameters.  Return qfalse if the game doesn't recognize it as a command.
        public bool Console_Command(string[] tokens)
        {
            return false;
        }


    }
}
