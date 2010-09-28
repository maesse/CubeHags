using System;
using System.Collections.Generic;
using System.Text;
using SlimDX;
using CubeHags.common;
using CubeHags.client;
using CubeHags.client.cgame;
using CubeHags.client.map.Source;

namespace CubeHags.server
{
    public sealed partial class Game
    {
        void respawn(gentity_t ent)
        {
            ClientSpawn(ent);
        }

        team_t PickTeam(int ignoreClientNum)
        {
            int red = TeamCount(ignoreClientNum, team_t.TEAM_RED);
            int blue = TeamCount(ignoreClientNum, team_t.TEAM_BLUE);

            if (red > blue)
                return team_t.TEAM_BLUE;
            if (blue > red)
                return team_t.TEAM_RED;
            // Same playercount, pick the team with least points
            if (level.teamScores[(int)team_t.TEAM_RED] > level.teamScores[(int)team_t.TEAM_BLUE])
                return team_t.TEAM_BLUE;
            return team_t.TEAM_RED;
        }

        int TeamCount(int ignoreClientNum, team_t team)
        {
            int count = 0;
            for (int i = 0; i < level.maxclients; i++)
            {
                if (i == ignoreClientNum)
                    continue;
                if (level.clients[i].pers.connected == clientConnected_t.CON_DISCONNECTED)
                    continue;
                if (level.clients[i].sess.sessionTeam == team)
                    count++;
            }
            return count;
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

            // Handicap
            string handi = Info.ValueForKey(info, "handicap");
            int hand;
            if (handi == null || handi.Length == 0)
                client.pers.maxHealth = 100;
            else if(int.TryParse(handi, out hand))
            {
                if (hand < 1 || hand > 100)
                    hand = 100;
                client.pers.maxHealth = hand;
            }
            client.ps.stats[6] = client.pers.maxHealth;

            string model = Info.ValueForKey(info, "model");
            team_t team = client.sess.sessionTeam;

            s = string.Format("n\\{0}\\t\\{1}\\hmodel\\{2}\\hc\\{3}\\w\\{4}\\l\\{5}", client.pers.netname, (int)client.sess.sessionTeam, model, client.pers.maxHealth, client.sess.wins, client.sess.losses);
            Server.Instance.SetConfigString((int)ConfigString.CS_PLAYERS + clientNum, s);

            // this is not the userinfo, more like the configstring actually
            //Common.Instance.WriteLine("ClientUserInfoChanged: {0} {1}", clientNum, s);
        }

        /*
       ===========
       ClientSpawn

       Called every time a client is placed fresh in the world:
       after the first ClientBegin, and after each respawn
       Initializes all non-persistant parts of playerState
       ============
       */
        
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
            Common.EntityFlags flags = ent.client.ps.eFlags & Common.EntityFlags.EF_TELEPORT_BIT;
            flags ^= Common.EntityFlags.EF_TELEPORT_BIT;

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
            string val = Info.ValueForKey(userinfo, "handicap");
            if (val != null && val.Length > 0)
            {
                int ival;
                if (int.TryParse(val, out ival))
                {
                    if (ival <= 0 || ival > 100)
                        client.pers.maxHealth = 100;
                    else
                        client.pers.maxHealth = ival;
                }
            } else 
                client.pers.maxHealth = 100;

            // clear entity values
            client.ps.stats[6] = client.pers.maxHealth;
            client.ps.eFlags = flags;

            ent.s.groundEntityNum = 1023; // none?
            //ent.client = level.clients[index];
            ent.takedamage = true;
            ent.inuse = true;
            ent.classname = "player";
            ent.r.contents = (int)brushflags.CONTENTS_MONSTER;
            ent.clipmask = (int)brushflags.MASK_PLAYERSOLID;
            ent.waterlevel = 0;
            // FIX: Add ent.die = player_die
            ent.flags = 0;
            ent.watertype = 0;
            ent.r.mins = Common.playerMins;
            ent.r.maxs = Common.playerMaxs;

            client.ps.clientNum = index;
            client.ps.stats[2] = 1 << 2;

            // health will count down towards max_health
            ent.health = client.ps.stats[0] = client.ps.stats[6] + 25;

            SetOrigin(ent, spawn_origin);
            client.ps.origin = spawn_origin;

            // the respawned flag will be cleared after the attack and jump keys come up
            client.ps.pm_flags |= PMFlags.RESPAWNED; // Respawned

            ent.client.pers.cmd = GetUserCommand(index);
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
    }
}
