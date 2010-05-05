using System;
using System.Collections.Generic;
 
using System.Text;
using CubeHags.common;

namespace CubeHags.server
{
    public sealed partial class Server
    {

        public void SetConfigString(int index, string p)
        {
            if (index < 0 || index >= 1025)
            {
                Common.Instance.Error("SetConfigString: Bad index " + index);
            }

            if (p == null)
                p = "";

            // don't bother broadcasting an update if no change
            if (sv.configstrings.ContainsKey(index) && sv.configstrings[index].Equals(p))
                return;

            // change the string in sv
            sv.configstrings[index] = p;

            // send it to all the clients if we aren't
            // spawning a new server
            if (sv.state == serverState_t.SS_GAME || sv.restarting)
            {
                // send the data to all relevent clients
                for (int i = 0; i < svs.clients.Count; i++)
                {
                    client_t cl = svs.clients[i];
                    if ((int)cl.state < (int)connstate_t.ACTIVE)
                    {
                        if (cl.state == clientState_t.CS_PRIMED)
                            cl.csUpdated[index] = true;
                        continue;
                    }

                    // do not always send server info to all clients
                    if (index == (int)ConfigString.CS_SERVERINFO && cl.gentity != null
                        && (cl.gentity.r.svFlags & Common.svFlags.NOSERVERINFO) == Common.svFlags.NOSERVERINFO)
                    {
                        continue;
                    }

                    SendConfigString(cl, index);
                }
            }
        }

        /*
        ===============
        SV_SendConfigstring

        Creates and sends the server command necessary to update the CS index for the
        given client
        ===============
        */
        void SendConfigString(client_t client, int index)
        {
            SendServerCommand(client, string.Format("cs {0} \"{1}\"\n", index, sv.configstrings[index]));
        }

        /*
        ===============
        SV_UpdateConfigstrings

        Called when a client goes from CS_PRIMED to CS_ACTIVE.  Updates all
        Configstring indexes that have changed while the client was in CS_PRIMED
        ===============
        */
        void UpdateConfigStrings(client_t cl)
        {
            for (int i = 0; i < cl.csUpdated.Length; i++)
            {
                // if the CS hasn't changed since we went to CS_PRIMED, ignore
                if (!cl.csUpdated[i])
                    continue;
                // do not always send server info to all clients
                if (i == (int)ConfigString.CS_SERVERINFO && cl.gentity != null 
                    && (cl.gentity.r.svFlags & Common.svFlags.NOSERVERINFO) == Common.svFlags.NOSERVERINFO)
                {
                    continue;
                }

                SendConfigString(cl, i);
                cl.csUpdated[i] = false;
            }
        }
    }
}
