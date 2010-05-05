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

            Common.Instance.WriteLine("Got Command: {0}", Commands.ArgsFrom(tokens, 0));
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

        private void ParseServerInfo()
        {
            string info = cgs.gameState.data[(int)ConfigString.CS_SERVERINFO];
            cgs.maxclients = int.Parse(Info.ValueForKey(info, "sv_maxclients"));
            cgs.mapname = Info.ValueForKey(info, "mapname");
        }
    }
}
