﻿using System;
using System.Collections.Generic;
using System.Text;
using CubeHags.common;
using CubeHags.client.common;
using CubeHags.client.gui;

namespace CubeHags.server
{
    public sealed partial class Server
    {
        void AddOperatorCommands()
        {
            Commands.Instance.AddCommand("status", new CommandDelegate(Status_f));
            Commands.Instance.AddCommand("serverinfo", new CommandDelegate(Serverinfo_f));
            Commands.Instance.AddCommand("systeminfo", new CommandDelegate(Systeminfo_f));
            Commands.Instance.AddCommand("map", new CommandDelegate(Map_f));
        }

        /*
        ==================
        SV_Map_f

        Restart the server on a different map
        ==================
        */
        void Map_f(string[] tokens)
        {
            if (tokens.Length < 2)
                return;

            // make sure the level exists before trying to change, so that
            // a typo at the server console won't end the game
            string expanded = string.Format("maps/{0}.bsp", tokens[1]);
            if (!FileCache.Instance.Contains(expanded))
            {
                Common.Instance.WriteLine("Can't find map {0}", expanded);
                return;
            }

            // start up the map
            SpawnServer(tokens[1]);
        }

        /*
        ===========
        SV_Serverinfo_f

        Examine the serverinfo string
        ===========
        */
        void Serverinfo_f(string[] tokens)
        {
            Common.Instance.WriteLine("Server info settings:");
            Info.Print(CVars.Instance.InfoString(CVarFlags.SERVER_INFO));
        }

        /*
        ===========
        SV_Systeminfo_f

        Examine or change the serverinfo string
        ===========
        */
        void Systeminfo_f(string[] tokens)
        {
            Common.Instance.WriteLine("System info settings:");
            Info.Print(CVars.Instance.InfoString(CVarFlags.SYSTEM_INFO));
        }

        void Status_f(string[] tokens)
        {
            // make sure server is running
            if (Common.Instance.sv_running.Integer == 0)
            {
                Common.Instance.WriteLine("Server is not running.");
                return;
            }

            Common.Instance.WriteLine("map: {0}", sv_mapname.String);
            Common.Instance.WriteLine("num score ping name                          lastmsg address                qport rate");
            Common.Instance.WriteLine("--- ----- ---- ----------------------------- ------- ---------------------- ----- -----");
            for (int i = 0; i < clients.Count; i++)
            {
                client_t cl = clients[i];
                if ((int)cl.state <= 0)
                    continue;
                string line = "";
                line += string.Format("{0,-3} ", i);
                Common.PlayerState ps = GameClientNum(i);
                line += string.Format("{0,-5} ", ps.persistant[0]);

                if (cl.state == clientState_t.CS_CONNECTED)
                    line += string.Format("CNCT ");
                else if (cl.state == clientState_t.CS_ZOMBIE)
                    line += string.Format("ZMBI ");
                else
                {
                    int ping = cl.ping < 9999 ? cl.ping : 9999;
                    line += string.Format("{0,-4} ", ping);
                }

                line += string.Format("{0,-29} ", cl.name);
                int nameDiff = cl.name.Length - HagsConsole.StripColors(cl.name).Length;
                for (int j = 0; j < nameDiff; j++)
                {
                    line += " ";
                }

                line += string.Format("^7{0,-7} ", time - cl.lastPacketTime);
                line += string.Format("{0,-22} ", cl.netchan.remoteAddress.ToString());
                line += string.Format("{0,-5} ", cl.netchan.qport);
                line += string.Format("{0,-5}", cl.rate);
                Common.Instance.WriteLine(line);
            }
            Common.Instance.WriteLine("");
        }
    }
}
