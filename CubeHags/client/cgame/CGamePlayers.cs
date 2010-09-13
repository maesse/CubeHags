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

            string configstring = CG_ConfigString((int)ConfigString.CS_PLAYERS + clientNum);
            if (configstring == null)
            {
                cgs.clientinfo[clientNum] = null;
                return; // player just left
            }

            // build into a temp buffer so the defer checks can use
            // the old value
            clientInfo_t newInfo = new clientInfo_t();
            string v = Info.ValueForKey(configstring, "n");
            newInfo.name = v;

            v = Info.ValueForKey(configstring, "t");
            newInfo.team = (team_t)Enum.Parse(typeof(team_t), v);

            v = Info.ValueForKey(configstring, "model");
            //LoadClientInfo(clientNum, ref newInfo); // load model

            newInfo.infoValid = true;
            cgs.clientinfo[clientNum] = newInfo;
        }
    }
}
