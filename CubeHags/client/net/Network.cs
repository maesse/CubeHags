using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace CubeHags.client
{
    class Network
    {
        private Socket socket;
        private int port;
        private EndPoint endPoint;
        private Encoding encoder = ASCIIEncoding.ASCII;

        public Network(string addr, int port)
        {
            this.port = port;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPAddress[] addresses = Dns.GetHostAddresses(addr); // Lookup hostname

            // Look for IPv4
            for (int i = 0; i < addresses.Length; i++)
            {
                IPAddress adr = addresses[i];
                if (adr.AddressFamily.Equals(AddressFamily.InterNetwork))
                {
                    endPoint = new IPEndPoint(addresses[i], port); // Set endpoint
                    break; // exit loop
                }
            }

            // Throw exception if we didn't find an ip..
            if (endPoint == null)
            {
                throw new Exception("Could not lookup hostname");
            }

            // Test
            byte[] text = encoder.GetBytes("Hello World! :)");
            SendPacket(text, text.Length, false);
        }

        // Sends a packet to the endpoint
        private void SendPacket(byte[] data, int size, bool reliable) 
        {
            socket.SendTo(data, size, SocketFlags.None, endPoint); // Send packet..
        }
    }
}
