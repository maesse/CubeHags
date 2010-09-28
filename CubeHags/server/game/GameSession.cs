using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.client.common;
using CubeHags.common;

namespace CubeHags.server
{
    public sealed partial class Game
    {
        void WriteClientSessionData(gclient_t client, int index)
        {
            string s = string.Format("{0} {1} {2} {3} {4} {5} {6}", (int)client.sess.sessionTeam, client.sess.spectatorTime, (int)client.sess.spectatorState, client.sess.spectatorClient, client.sess.wins, client.sess.losses, client.sess.teamLeader?1:0);

            string var = string.Format("session{0}", index);

            CVars.Instance.Set(var, s);
        }

        void ReadSessionData(gclient_t client, int index)
        {
            string var = string.Format("session{0}", index);
            string s = CVars.Instance.VariableString(var);

            string[] tokens = s.Split(' ');
            if (tokens.Length != 7)
            {
                Common.Instance.WriteLine("");
                return;
            }
            int iteam;
            if (int.TryParse(tokens[0], out iteam))
                client.sess.sessionTeam = (team_t)iteam;
            int ispec;
            if (int.TryParse(tokens[2], out ispec))
                client.sess.spectatorState = (spectatorState_t)ispec;
            client.sess.teamLeader = (tokens[6] == "1") ? true : false;

        }


        /*
        ================
        G_InitSessionData

        Called on a first-time connect
        ================
        */
        void InitSessionData(gclient_t client, int index, string userinfo)
        {
            clientSession_t sess = client.sess;

            if (g_gametype.Integer >= (int)GameType.TEAM)
            {
                if (g_teamAutoJoin.Bool)
                {
                    sess.sessionTeam = PickTeam(-1);
                    BroadcastTeamChange(client, team_t.TEAM_NUM_TEAMS);
                }
                else // always spawn as spectator in team games
                    sess.sessionTeam = team_t.TEAM_SPECTATOR;
            }
            else
            {
                string value = Info.ValueForKey(userinfo, "team");
                if (value == "s") // A willing spectator
                    sess.sessionTeam = team_t.TEAM_SPECTATOR;
                else
                {
                    switch ((GameType)g_gametype.Integer)
                    {
                        default:
                        case GameType.FFA:
                            if (g_maxGameClients.Integer > 0 && level.numNonSpectatorClients >= g_maxGameClients.Integer)
                                sess.sessionTeam = team_t.TEAM_SPECTATOR;
                            else
                                sess.sessionTeam = team_t.TEAM_FREE;
                            break;
                        case GameType.TOURNAMENT:
                            // if the game is full, go into a waiting mode
                            if (level.numNonSpectatorClients >= 2)
                                sess.sessionTeam = team_t.TEAM_SPECTATOR;
                            else
                                sess.sessionTeam = team_t.TEAM_FREE;
                            break;
                    }
                }
            }

            sess.spectatorState = spectatorState_t.SPECTATOR_FREE;
            sess.spectatorTime = (int)level.time;

            client.sess = sess;

            WriteClientSessionData(client, index);
        }

        void WriteSessionData()
        {
            //CVars.Instance.Set("session", string.Format("{0}", 1));

            for (int i = 0; i < level.maxclients; i++)
            {
                if (level.clients[i] == null)
                    continue;
                if (level.clients[i].pers.connected == clientConnected_t.CON_CONNECTED)
                {
                    WriteClientSessionData(level.clients[i], i);
                }
            }
        }
    }
}
