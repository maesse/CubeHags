using System;
using System.Collections.Generic;
 
using System.Text;
using System.Net;
using Lidgren.Network;
using CubeHags.client.common;

namespace CubeHags.common
{
    public sealed partial class Net
    {
        struct packetQueue_t
        {
            public Net.Packet packet;
            public int release;
        }

        public class netchan_t
        {
            public int dropped;			// between last packet and previous

            public IPEndPoint remoteAddress;
            public int qport;				// qport value to write when transmitting

            public NetConnection connection;
            public NetSource sock;

            // sequencing variables
            public int incomingSequence;
            public int outgoingSequence;
        }

        Queue<packetQueue_t> packetQueue = new Queue<packetQueue_t>();
        CVar qport;

        public netchan_t NetChan_Setup(NetSource source, IPEndPoint from, int qport)
        {
            netchan_t netchan = new netchan_t();
            netchan.remoteAddress = from;
            netchan.qport = qport;
            netchan.outgoingSequence = 1;
            netchan.sock = source;
            return netchan;
        }


        /*
        =================
        Netchan_Process

        Returns qfalse if the message should not be processed due to being
        out of order or a fragment.

        Msg must be large enough to hold MAX_MSGLEN, because if this is the
        final fragment of a multi-part message, the entire thing will be
        copied out.
        =================
        */
        public bool NetChan_Process(netchan_t chan, Packet packet)
        {
            // get sequence numbers	
            int sequence = packet.Buffer.ReadInt32();

            // check for fragment information
            bool fragmented = false;
            if ((sequence & (1 << 31)) == (1 << 31))
            {
                sequence &= ~(1 << 31);
                fragmented = true;
            }

            // read the qport if we are a server
            int qport;
            if (chan.sock == NetSource.SERVER)
            {
                qport = packet.Buffer.ReadInt16();
            }

            // read the fragment information
            int fragStart = 0, fragEnd = 0;
            if (fragmented)
            {
                fragStart = packet.Buffer.ReadInt16();
                fragEnd = packet.Buffer.ReadInt16();
            }

            //
            // discard out of order or duplicated packets
            //
            if (sequence <= sequence - (chan.incomingSequence + 1))
            {
                Common.Instance.WriteLine("[0}: out of order packetseq {1} at {2}", packet.Address.ToString(), sequence, chan.incomingSequence);
                return false;
            }

            //
            // dropped packets don't keep the message from being used
            //
            chan.dropped = sequence - (chan.incomingSequence + 1);
            if (chan.dropped > 0)
            {
                Common.Instance.WriteLine("{0}: dropped {1} packets at {2}", packet.Address.ToString(), chan.dropped, sequence);
            }

            //
            // if this is the final framgent of a reliable message,
            // bump incoming_reliable_sequence 
            //
            if (fragmented)
            {
                Common.Instance.WriteLine("Implement fragmentation");
            }

            //
            // the message can now be read from the current message pointer
            //
            chan.incomingSequence = sequence;
            return true;
        }

        /*
        ===============
        Netchan_Transmit

        Sends a message to a connection, 
        A 0 length will still generate a packet.
        ================
        */
        public void NetChan_Transmit(netchan_t chan, NetBuffer data)
        {
            NetBuffer buf = new NetBuffer();
            // write the packet header
            buf.Write(chan.outgoingSequence);
            chan.outgoingSequence++;

            // send the qport if we are a client
            if (chan.sock == NetSource.CLIENT)
            {
                buf.Write((short)qport.Integer);
            }

            buf.Write(data.Data);

            // send the datagram
            SendPacket(chan.sock, buf, chan.connection);
        }

        
    }
}
