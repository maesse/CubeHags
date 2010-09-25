using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.common;

namespace CubeHags.server
{
    public sealed partial class Game
    {
        public void Client_Command(int clientNum, string[] tokens)
        {
            gentity_t ent = g_entities[clientNum];
            if (ent.client == null)
                return; // not fully in game yet

            if (tokens == null || tokens.Length == 0)
                return;
            //tokens[0] = tokens[0].ToLower();
            string cmd = tokens[0].ToLower();
            if (cmd.Equals("say"))
            {
                Cmd_Say_f(ent, SayMode.ALL, tokens, false);
                return;
            }
            if (cmd.Equals("say_team"))
            {
                Cmd_Say_f(ent, SayMode.TEAM, tokens, false);
                return;
            }
            if (cmd.Equals("tell"))
            {
                //Cmd_Tell_f(ent);
            }

            // Ignore other commands when in intermission time..
            if (level.intermissiontime != 0)
            {
                Cmd_Say_f(ent, SayMode.ALL, tokens, true);
                return;
            }

            switch (cmd)
            {
                case "god":
                    Cmd_God_f(ent);
                    break;
                case "kill":
                    Cmd_Kill_f(ent);
                    break;
                case "team":
                    Cmd_Team_f(ent, clientNum, tokens);
                    break;
                default:
                    Server.Instance.SendServerCommand(Server.Instance.clients[clientNum], string.Format("print \"unknown cmd {0}\n\"", cmd));
                    break;
            }

        }

        void Cmd_Kill_f(gentity_t ent)
        {
            if (ent.client.sess.sessionTeam == team_t.TEAM_SPECTATOR)
                return;

            if (ent.health <= 0)
                return;

            ent.flags &= ~gentityFlags.FL_GODMODE;
            ent.client.ps.stats[0] = ent.health = -999;
            player_die(ent, ent, ent, 100000, 0);
        }

        public enum MeansOfDeath : int
        {
            UNKNOWN,
            SUICIDE,
            FALLING,
            TRIGGER_HURT
        }

        void player_die(gentity_t self, gentity_t inflictor, gentity_t attacker, int damage, MeansOfDeath mod)
        {
            if (self.client.ps.pm_type == Common.PMType.DEAD)
                return;

            if (level.intermissiontime > 0)
                return;

            self.client.ps.pm_type = Common.PMType.DEAD;
            int killer;
            string killerName = "";
            if (attacker != null)
            {
                killer = attacker.s.number;
                if (attacker.client != null)
                    killerName = attacker.client.pers.netname;
                else
                    killerName = "<non-client>";
            }
            else
            {
                killer = 1022;
                killerName = "<world>";
            }

            if (killer < 0 || killer >= 64)
            {
                killer = 1022;
                killerName = "<world>";
            }

            string obit = Enum.GetName(typeof(MeansOfDeath), mod);

            LogPrintf("Kill: {0} {1}: {3} killed {4} by {5}\n", killer, self.s.number, killerName, self.client.pers.netname, obit);

            self.enemy = attacker;

            // FIX: Add score

            Cmd_Score_f(self); // Score scores
            // send updated scores to any clients that are following this one,
            // or they would get stale scoreboards
            for (int j = 0; j < level.maxclients; j++)
            {
                gclient_t client;
                client = level.clients[j];

                if (client.pers.connected != clientConnected_t.CON_CONNECTED)
                    continue;

                if (client.sess.sessionTeam != team_t.TEAM_SPECTATOR)
                    continue;
                if (client.sess.spectatorClient == self.s.number)
                    Cmd_Score_f(g_entities[j]);
            }

            self.client.respawnTime = (int)level.time + 1700;
            Server.Instance.LinkEntity(GEntityToSharedEntity(self));
        }

        /*
        ==================
        Cmd_Score_f

        Request current scoreboard information
        ==================
        */
        void Cmd_Score_f(gentity_t ent)
        {

        }

        bool CheatsOk(gentity_t ent)
        {
            if (!g_cheats.Bool)
            {
                Server.Instance.SendServerCommand(Server.Instance.clients[ent.client.clientIndex], string.Format("print \"Cheats are not enabled on this server.\n\""));
                return false;
            }
            if (ent.health <= 0)
            {
                Server.Instance.SendServerCommand(Server.Instance.clients[ent.client.clientIndex], string.Format("print \"You must be alive to use this command.\n\""));
                return false;
            }

            return true;
        }

        void Cmd_God_f(gentity_t ent)
        {
            if (!CheatsOk(ent))
                return;

            string msg;

            ent.flags ^= gentityFlags.FL_GODMODE;
            if ((ent.flags & gentityFlags.FL_GODMODE) == gentityFlags.FL_GODMODE)
                msg = "godmode ON\n";
            else
                msg = "godmode OFF\n";

            Server.Instance.SendServerCommand(Server.Instance.clients[ent.client.clientIndex], string.Format("pring \"{0}\"", msg));
        }

        void G_Say(gentity_t ent, gentity_t target, SayMode mode, string chatText)
        {

            string name = null;
            string color = "";

            switch (mode)
            {
                default:
                case SayMode.ALL:
                    LogPrintf("say: {0}: {1}\n", ent.client.pers.netname, chatText);
                    name = string.Format("{0}{1}{2}: ", ent.client.pers.netname, "^7", (char)0x19);
                    color = "2";
                    break;
                case SayMode.TEAM:
                    LogPrintf("sayteam: {0}: {1}\n", ent.client.pers.netname, chatText);
                    name = string.Format("{0}({1}{2}{3}){4}: ", (char)0x19, ent.client.pers.netname, "^7", (char)0x19, (char)0x19);
                    color = "5";
                    break;
                case SayMode.TELL:
                    name = string.Format("{0}[{1}{2}{2}]{2}", (char)0x19, ent.client.pers.netname, "^7",(char)0x19,(char)0x19);
                    color = "6";
                    break;
            }

            if (target != null)
            {
                G_SayTo(ent, target, mode, color, name, chatText);
                return;
            }

            // send it to all the apropriate clients
            gentity_t other;
            for (int i = 0; i < level.maxclients; i++)
            {
                other = g_entities[i];
                G_SayTo(ent, other, mode, color, name, chatText);
            }
        }

        void G_SayTo(gentity_t ent, gentity_t other, SayMode mode, string color, string name, string message)
        {
            if (other == null || !other.inuse || other.client == null || other.client.pers.connected != clientConnected_t.CON_CONNECTED || (mode == SayMode.TEAM && !OnSameTeam(ent, other)))
                return;

            Server.Instance.SendServerCommand(Server.Instance.clients[other.client.clientIndex], string.Format("{0} \"{1}{2}{3}\"", (mode == SayMode.TEAM) ? "tchat" : "chat", name, "^" + color, message));
        }

        void Cmd_Say_f(gentity_t ent, SayMode mode, string[] tokens, bool arg0)
        {
            if (tokens.Length < 2 && !arg0)
                return;

            string p;
            if (arg0)
                p = CubeHags.client.common.Commands.ArgsFrom(tokens, 0);
            else
                p = CubeHags.client.common.Commands.ArgsFrom(tokens, 1);

            G_Say(ent, null, mode, p);
        }

        void Cmd_Team_f(gentity_t ent, int entNum, string[] tokens)
        {
            if (tokens.Length != 2)
            {
                //TODO: Print current team to client
                return;
            }

            if (ent.client.switchTeamTime > (int)level.time)
            {
                ///Server.Instance.SendServerCommand(ent.client, "print \"May not switch teams more than once per 5 seconds\n\"");
                return;
            }

            SetTeam(ent, entNum, tokens[1]);
            ent.client.switchTeamTime = (int)level.time + 5000;
        }

        void SetTeam(gentity_t ent, int entNum, string s)
        {
            //
            // see what change is requested
            //
            gclient_t client = ent.client;
            team_t team = team_t.TEAM_FREE;
            team_t oldTeam = client.sess.sessionTeam;
            s = s.ToLower();
            switch (s)
            {
                case "spectator":
                    team = team_t.TEAM_SPECTATOR;
                    break;
                case "red":
                    team = team_t.TEAM_RED;
                    break;
                case "blue":
                    team = team_t.TEAM_BLUE;
                    break;
            }

            //
            // execute the team change
            //
            client.sess.sessionTeam = team;

            BroadcastTeamChange(client, oldTeam);

            // get and distribute relevent paramters
            ClientUserInfoChanged(entNum);

            Client_Begin(entNum);
        }

        void BroadcastTeamChange(gclient_t client, team_t oldTeam)
        {
            switch (client.sess.sessionTeam)
            {
                case team_t.TEAM_FREE:
                    Server.Instance.SendServerCommand(null, string.Format("print \"{0} joined the battle.\n\"", client.pers.netname));
                    break;
                case team_t.TEAM_SPECTATOR:
                    Server.Instance.SendServerCommand(null, string.Format("print \"{0} joined the spectators.\n\"", client.pers.netname));
                    break;
                case team_t.TEAM_RED:
                    Server.Instance.SendServerCommand(null, string.Format("print \"{0} joined the red team.\n\"", client.pers.netname));
                    break;
                case team_t.TEAM_BLUE:
                    Server.Instance.SendServerCommand(null, string.Format("print \"{0} joined the blue team.\n\"", client.pers.netname));
                    break;
            }
        }
    }
}
