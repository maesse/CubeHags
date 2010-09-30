using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using SlimDX;
using CubeHags.client;
using CubeHags.client.cgame;
using CubeHags.client.common;
using CubeHags.client.map.Source;

namespace CubeHags.server
{
    public delegate void ThinkDelegate(gentity_t ent);
    public sealed partial class Game
    {
        private static readonly Game _Instance = new Game();
        public static Game Instance {get {return _Instance;}}

        CVar sv_gravity;
        CVar sv_speed;
        CVar g_synchrounousClients;
        CVar g_smoothClients;
        CVar g_cheats;
        CVar g_gametype;
        CVar g_teamAutoJoin; // force team when client joins
        CVar g_maxGameClients; // Max non-spec clients
        CVar g_forcerespawn;
        CVar g_edgefriction;

        public level_locals_t level;
        public gentity_t[] g_entities;
        public gclient_t[] g_clients;

        public Game()
        {
            spawns  = new spawn_t[] { new spawn_t{Name = "info_player_start", Spawn = new SpawnDelegate(SP_info_player_start)} };
            sv_gravity = CVars.Instance.Get("sv_gravity", "800", CVarFlags.SERVER_INFO);
            sv_speed = CVars.Instance.Get("sv_speed", "400", CVarFlags.SERVER_INFO);
            g_synchrounousClients = CVars.Instance.Get("g_synchrounousClients", "0", CVarFlags.SERVER_INFO);
            g_forcerespawn = CVars.Instance.Get("g_forcerespawn", "20", CVarFlags.NONE);
            g_smoothClients = CVars.Instance.Get("g_smoothClients", "0", CVarFlags.SERVER_INFO);
            g_gametype = CVars.Instance.Get("g_gametype", "0", CVarFlags.SERVER_INFO | CVarFlags.LATCH | CVarFlags.USER_INFO);
            g_teamAutoJoin = CVars.Instance.Get("g_teamAutoJoin", "0", CVarFlags.ARCHIVE);
            g_maxGameClients = CVars.Instance.Get("g_maxGameClients", "0", CVarFlags.SERVER_INFO | CVarFlags.ARCHIVE | CVarFlags.LATCH);
            g_edgefriction = CVars.Instance.Get("g_edgefriction", "2", CVarFlags.SERVER_CREATED);
        }

        public void LogPrintf(string fmt, params object[] data)
        {
            string.Format(fmt, data);
        }

        public void LogPrintf(string str)
        {
            Common.Instance.WriteLine("G: " + str);
        }

        public Server.svEntity_t SvEntityForGentity(sharedEntity gEnt) 
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

            ent.client.ps.stats[0] = ent.health;

            // set the latest infor
            CGame.PlayerStateToEntityState(ent.client.ps, ent.s, true);
            SendPredictableEvents(ent.client.ps);
        }

        private void SendPredictableEvents(Common.PlayerState ps)
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
                t.s.eFlags |= Common.EntityFlags.EF_BOUNCE;
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
            Server.Instance.LocateGameData(level.sentities, level.num_entities, level.clients);
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
            string userinfo = Server.Instance.clients[clientNum].userinfo;

            // they can connect
            level.clients[clientNum] = new gclient_t();
            ent.client = level.clients[clientNum];
            gclient_t client = ent.client;
            client.clientIndex = clientNum;
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


        

        string ClientCleanName(string s)
        {
            string res = s.Trim();
            if (res.Length == 0)
                res = "UnnamedPlayer";

            return res;
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

        



        private void RunClient(gentity_t ent)
        {
            if (g_synchrounousClients.Integer == 0)
                return;
            ent.client.pers.cmd.serverTime = (int)level.time;
            Client_Think(ent);
        }

        


        public void Client_Think(int clientNum)
        {
            gentity_t ent = g_entities[clientNum];
            ent.client.pers.cmd = GetUserCommand(clientNum);
            ent.client.lastCmdTime = (int)level.time;

            if(g_synchrounousClients.Integer == 0)
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
            gclient_t client = ent.client;

            // don't think if the client is not yet connected (and thus not yet spawned in)
            if (client.pers.connected != clientConnected_t.CON_CONNECTED)
                return;

            Input.UserCommand ucmd = ent.client.pers.cmd;
            // sanity check the command time to prevent speedup cheating
            if(ucmd.serverTime > level.time + 200)
                ucmd.serverTime = (int)level.time + 200;
            if (ucmd.serverTime < level.time - 1000)
                ucmd.serverTime = (int)level.time - 1000;

            // mark the time we got info, so we can display the
            //ent.client.pers.cmd = Server.Instance.GetUsercmd(ent.s.clientNum);

            // phone jack if they don't get any for a while
            ent.client.lastCmdTime = (int)level.time;

            int msec = ucmd.serverTime - ent.client.ps.commandTime;
            // following others may result in bad times, but we still want
            // to check for follow toggles
            if (msec < 1 && ent.client.sess.spectatorState != spectatorState_t.SPECTATOR_FOLLOW)
                return;
            if (msec > 200)
                msec = 200;

            CVar pmove_msec = CVars.Instance.FindVar("pmove_msec");
            if (pmove_msec.Integer < 8)
                CVars.Instance.Set("pmove_msec", "8");
            else if (pmove_msec.Integer > 33)
                CVars.Instance.Set("pmove_msec", "33");

            if (CVars.Instance.FindVar("pmove_fixed").Bool || client.pers.pmoveFixed)
            {
                ucmd.serverTime = ((ucmd.serverTime + pmove_msec.Integer - 1) / pmove_msec.Integer) * pmove_msec.Integer;
            }

            //
            // check for exiting intermission
            //
            if (level.intermissiontime > 0)
            {
                //ClientIntermissionThink(client);
                return;
            }

            // spectators don't do much
            if (client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
            {
                if (client.sess.spectatorState == spectatorState_t.SPECTATOR_SCOREBOARD)
                    return;
                //client.ps.speed = sv_speed.Integer;
                SpectatorThink(ent, ucmd);
                return;
            }

            // check for inactivity timer, but never drop the local client of a non-dedicated server
            //if (!ClientInactivityTimer(ent.client))
            //    return;

            if (client.noclip)
                client.ps.pm_type = Common.PMType.NOCLIP;
            else if (client.ps.stats[0] <= 0)
                client.ps.pm_type = Common.PMType.DEAD;
            else
                client.ps.pm_type = Common.PMType.NORMAL;

            // Gravity & speed
            client.ps.gravity = sv_gravity.Integer;
            client.ps.speed = sv_speed.Integer;

            // Set up for pmove
            int oldEventSequence = client.ps.eventSequence;
            pmove_t pm = new pmove_t();
               //#define	MASK_ALL				(-1)
    //#define	MASK_SOLID				(1)
    //#define	MASK_PLAYERSOLID		(1|0x10000|0x2000000)
    //#define	MASK_DEADSOLID			(1|0x10000)
    //#define	MASK_WATER				(32)
    //#define	MASK_OPAQUE				(1)
    //#define	MASK_SHOT				(1|0x2000000|0x4000000)
            pm.Trace = new TraceDelegate(ClipMap.Instance.SV_Trace);
            pm.ps = client.ps;
            pm.cmd = ucmd;
            if (pm.ps.pm_type == Common.PMType.DEAD)
                pm.tracemask = (int)brushflags.MASK_PLAYERSOLID & ~(int)brushflags.CONTENTS_MONSTER;
            else
                pm.tracemask = (int)brushflags.MASK_PLAYERSOLID;
            pm.pmove_fixed = ((CVars.Instance.FindVar("pmove_fixed").Bool) | client.pers.pmoveFixed)?1:0;
            pm.pmove_msec = pmove_msec.Integer;
            client.oldOrigin = client.ps.origin;
            //pm.mins = Common.playerMins;
            //pm.maxs = Common.playerMaxs;
            Common.Instance.Pmove(pm);


            //client.ps.pm_type = Common.PMType.SPECTATOR;
            //client.ps.speed = 400;  // faster than normal

            
            
            

            // save results of pmove
            if (ent.client.ps.eventSequence != oldEventSequence)
                ent.eventTime = (int)level.time;
            if (g_smoothClients.Integer == 1)
            {

            }
            else
            {
                CGame.PlayerStateToEntityState(ent.client.ps, ent.s, true);
            }
            SendPredictableEvents(ent.client.ps);

            ent.r.currentOrigin = ent.s.pos.trBase;
            ent.r.mins = pm.mins;
            ent.r.maxs = pm.maxs;

            // execute client events
            //ClientEvents(ent, oldEventSequence);

            // link entity now, after any personal teleporters have been used
            Server.Instance.LinkEntity(GEntityToSharedEntity(ent));
            if (!ent.client.noclip)
            {
                //TouchTriggers(ent);
            }

            // NOTE: now copy the exact origin over otherwise clients can be snapped into solid
            ent.r.currentOrigin = ent.client.ps.origin;

            // touch other objects
            //ClientImpacts(ent, pm);

            // save results of triggers and client events
            if (ent.client.ps.eventSequence != oldEventSequence)
            {
                ent.eventTime = (int)level.time;
            }

            // swap and latch button actions
            client.oldbuttons = client.buttons;
            client.buttons = ucmd.buttons;
            client.latched_buttons |= client.buttons & ~client.oldbuttons;

            // check for respawning
            if (client.ps.stats[0] <= 0)
            {
                // wait for the attack button to be pressed
                if (level.time > client.respawnTime)
                {
                    // forcerespawn is to prevent users from waiting out powerups
                    if (g_forcerespawn.Integer > 0 && level.time - client.respawnTime > g_forcerespawn.Integer * 1000)
                    {
                        respawn(ent);
                        return;
                    }

                    // pressing attack or use is the normal respawn method
                    if ((ucmd.buttons & ((int)Input.ButtonDef.ATTACK | (int)Input.ButtonDef.USE)) > 0)
                        respawn(ent);

                    
                }
                return;
            }

            ClientTimerActions(ent, msec);
        }

        void ClientTimerActions(gentity_t ent, int msec)
        {
            gclient_t client = ent.client;
            client.timeResidual += msec;

            while (client.timeResidual >= 1000)
            {
                client.timeResidual -= 1000;

                // count down health when over max
                if (ent.health > client.ps.stats[6])
                    ent.health--;
                // count down armor when over max
                if (client.ps.stats[3] > client.ps.stats[6])
                    client.ps.stats[3]--;
            }
        }

        void SpectatorThink(gentity_t ent, Input.UserCommand ucmd)
        {
            gclient_t client = ent.client;
            if (client.sess.spectatorState != spectatorState_t.SPECTATOR_FOLLOW)
            {
                client.ps.pm_type = Common.PMType.SPECTATOR;
                client.ps.speed = sv_speed.Integer;  // faster than normal
                
                // set up for pmove
                pmove_t pm = new pmove_t();
                pm.Trace = new TraceDelegate(ClipMap.Instance.SV_Trace);
                pm.ps = client.ps;
                pm.cmd = ucmd;
                pm.tracemask = (int)(brushflags.CONTENTS_SOLID | brushflags.CONTENTS_MOVEABLE | brushflags.CONTENTS_SLIME | brushflags.CONTENTS_OPAQUE);
                pm.pmove_fixed = CVars.Instance.VariableIntegerValue("pmove_fixed");
                pm.pmove_msec = CVars.Instance.VariableIntegerValue("pmove_msec");
                //pm.Trace += new TraceDelegate(Server.Instance.Trace);
                //pm.PointContents += new TraceContentsDelegate(Server.Instance.PointContents);
                pm.mins = Common.playerMins;
                pm.maxs = Common.playerMaxs;
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
            Common.EntityFlags flags = client.ps.eFlags;
            client.ps = new Common.PlayerState();
            client.ps.eFlags = flags;

            // locate ent at a spawn point
            ClientSpawn(ent);

            Server.Instance.LocateGameData(level.sentities, level.num_entities, level.clients);

            if (client.sess.sessionTeam != team_t.TEAM_SPECTATOR)
            {
                // send event
                //gentity_t tent = TempEntity(ent.client.ps.origin, 42); // EV_PLAYER_TELEPORT_IN
                //tent.s.clientNum = ent.s.clientNum;

                Server.Instance.SendServerCommand(null, string.Format("print \"{0} entered the game\n\"", client.pers.netname));
            }

            //Common.Instance.WriteLine("Client_Begin {0}", clientNum);
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
            int tc = -1;
            while ((t = Find(ref tc, "targetname", ent.target)) != null)
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
            if (index < 0 || index >= Server.Instance.clients.Count)
            {
                Common.Instance.Error(string.Format("GetUserCommand: bad clientNum: {0}", index));
            }
            return Server.Instance.clients[index].lastUsercmd;
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
            return Server.Instance.clients[index].userinfo;
        }

        void InitEntity(gentity_t ent)
        {
            ent.inuse = true;
            ent.classname = "noclass";
            int entid = 1024;
            for (int i = 0; i < g_entities.Length; i++)
            {
                if (g_entities[i] == ent)
                    entid = i;
            }
            ent.s.number = entid;
            ent.r.ownerNum = 1023; // None?
        }

        public sharedEntity GEntityToSharedEntity(gentity_t ent)
        {
            //sharedEntity sent = new sharedEntity();
            //sent.s = ent.s;
            //sent.r = ent.r;
            return ent.shEnt;
        }

        public void Init(float levelTime, int randSeed, int restart)
        {
            LogPrintf("----- Game Initialization -----");

            g_cheats = CVars.Instance.Get("g_cheats", "", CVarFlags.NONE);

            level.time = levelTime;
            level.startTime = levelTime;
            level.newSession = true;

            // initialize all entities for this game
            g_entities = new gentity_t[1024];
            level.sentities = new sharedEntity[1024];
            for (int i = 0; i < 1024; i++)
			{
                g_entities[i] = (new gentity_t());
                level.sentities[i] = g_entities[i].shEnt;
			}
            level.gentities = g_entities;

            // initialize all clients for this game
            level.maxclients = 32;
            g_clients = new gclient_t[64];
            for (int i = 0; i < level.maxclients; i++)
            {
                g_clients[i] = (new gclient_t());
            }
            level.clients = g_clients;

            // set client fields on player ents
            for (int i = 0; i < level.maxclients; i++)
            {
                gentity_t ent = g_entities[i];
                ent.s.number = i;
                ent.s.clientNum = i;
                ent.client = level.clients[i];
            }

            // always leave room for the max number of clients,
            // even if they aren't all used, so numbers inside that
            // range are NEVER anything but clients
            level.num_entities = 64;
            
            Server.Instance.LocateGameData(level.sentities, level.num_entities, level.clients);

            SpawnEntitiesFromString();

            FindTeams();
        }

        public void ShutdownGame(int restart)
        {
            LogPrintf("==== ShutdownGame ====\n");

            // Write session data, so we can get it back
            WriteSessionData();
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
