using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CubeHags.common;
using Lidgren.Network;

namespace CubeHags.client
{
    public sealed partial class Client
    {
        public void NetChan_Transmit(Net.netchan_t chan, NetBuffer msg) 
        {
            msg.Write((byte)clc_ops_e.clc_EOF);
            Net.Instance.NetChan_Transmit(chan, msg);
        }

        bool NetChan_Process(Net.netchan_t chan, Net.Packet packet)
        {
            bool ret = Net.Instance.NetChan_Process(chan, packet);
            return ret;
        }


    }
}
