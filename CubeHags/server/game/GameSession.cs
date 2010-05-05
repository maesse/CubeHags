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
                return;

            client.sess.sessionTeam = (team_t)Enum.Parse(typeof(team_t), tokens[0]);
            client.sess.spectatorState = (spectatorState_t)Enum.Parse(typeof(spectatorState_t), tokens[2]);
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
            client.sess.sessionTeam = team_t.TEAM_SPECTATOR;
            client.sess.spectatorState = spectatorState_t.SPECTATOR_FREE;
            client.sess.spectatorTime = (int)level.time;

            WriteClientSessionData(client, index);
        }

        void WriteSessionData()
        {
            //CVars.Instance.Set("session", string.Format("{0}", 1));

            for (int i = 0; i < level.maxclients; i++)
            {
                if (level.clients[i].pers.connected == clientConnected_t.CON_CONNECTED)
                {
                    WriteClientSessionData(level.clients[i], i);
                }
            }
        }
    }
}
