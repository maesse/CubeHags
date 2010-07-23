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

            tokens[0] = tokens[0].ToLower();
            switch (tokens[0])
            {
                case "team":
                    Cmd_Team_f(ent, clientNum, tokens);
                    break;
            }

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
