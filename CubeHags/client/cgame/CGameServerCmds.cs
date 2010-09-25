using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;
using CubeHags.client.common;

namespace CubeHags.client.cgame
{
    public sealed partial class CGame
    {
        void ServerCommand(string[] tokens)
        {
            string command = tokens[0];
            if (command.Equals("print"))
            {
                Common.Instance.WriteLine(Commands.Args(tokens));
                return;
            }
            
            if (command.Equals("cs"))
            {
                ConfigStringModified(tokens);
                return;
            }

            if (command.Equals("cp"))
            {
                // Centerprint
                return;
            }

            if (command.Equals("chat"))
            {
                string text = Commands.Args(tokens);
                text = RemoveChatEscapeChar(text);
                Common.Instance.Printf(text+"\n");
                return;
            }

            //if (command.Equals("map_restart"))
            //{
            //    MapRestart();
            //    return;
            //}
            Common.Instance.WriteLine("Got Command: {0}", Commands.ArgsFrom(tokens, 0));
        }

        string RemoveChatEscapeChar(string text)
        {
            int i;
            StringBuilder str = new StringBuilder(text.Length);
            for (i = 0; i < text.Length; i++)
            {
                if (text[i] == (char)0x19)
                    continue;
                str.Append(text[i]);
            }
            return str.ToString();
        }

        void ConfigStringModified(string[] tokens)
        {
            int num; 
            if (!int.TryParse(tokens[1], out num))
                return;

            // get the gamestate from the client system, which will have the
            // new configstring already integrated
            cgs.gameState = Client.Instance.cl.gamestate;

            // look up the individual string that was modified
            string str = CG_ConfigString(num);

            // do something with it if necessary
            if (num == (int)ConfigString.CS_SERVERINFO)
                ParseServerInfo();
            else if (num == (int)ConfigString.CS_LEVEL_START_TIME)
                cgs.levelStartTime = int.Parse(str);
            else if (num >= (int)ConfigString.CS_PLAYERS && num < (int)ConfigString.CS_PLAYERS + 64)
            {
                NewClientInfo(num - (int)ConfigString.CS_PLAYERS);
            }
        }

        void ExecuteNewServerCommands(int latestSequence)
        {
            while (cgs.serverCommandSequence < latestSequence)
            {
                string[] tokens;
                if ((tokens = Client.Instance.GetServerCommand(++cgs.serverCommandSequence)) != null)
                {
                    ServerCommand(tokens);
                }
            }
        }

        /*
        ================
        CG_SetConfigValues

        Called on load to set the initial values from configure strings
        ================
        */
        private void SetConfigValues()
        {
            cgs.levelStartTime = int.Parse(CG_ConfigString((int)ConfigString.CS_LEVEL_START_TIME));
        }

        private void ParseServerInfo()
        {
            string info = cgs.gameState.data[(int)ConfigString.CS_SERVERINFO];
            cgs.maxclients = int.Parse(Info.ValueForKey(info, "sv_maxclients"));
            cgs.mapname = string.Format("maps/{0}.bsp", Info.ValueForKey(info, "mapname"));
        }
    }
}
