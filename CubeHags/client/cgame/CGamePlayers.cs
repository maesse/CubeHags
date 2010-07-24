using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.common;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        void NewClientInfo(int clientNum)
        {
            clientInfo_t ci = cgs.clientinfo[clientNum];

            if (!cgs.gameState.data.ContainsKey(544 + clientNum))
            {
                ci = new clientInfo_t();
                return; // player just left
            }
            string configString = cgs.gameState.data[544 + clientNum];

            // build into a temp buffer so the defer checks can use
            // the old value
            clientInfo_t newInfo = new clientInfo_t();
            string v = Info.ValueForKey(configString, "n");
            newInfo.name = v;

            v = Info.ValueForKey(configString, "t");
            newInfo.team = (team_t)Enum.Parse(typeof(team_t), v);

            //LoadClientInfo(clientNum, ref newInfo);

            newInfo.infoValid = true;
            cgs.clientinfo[clientNum] = newInfo;
        }
    }
}
