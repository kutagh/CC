using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Port = System.Int16;

namespace CC {
    public class Row {
        /// <summary>
        /// Distance from node u to node v
        /// </summary>
        public int Du;

        /// <summary>
        /// Preferred neighbor w of node u to reach node v
        /// </summary>
        public Neighbor NBu;

        /// <summary>
        /// Dictionary that contains all distances of other nodes to node v, as known by node u
        /// </summary>
        public Dictionary<Port, int> NDISu = new Dictionary<Port, int>();

        public void SendMessage(string message) {
            if (NBu == null) // Can't send whatsoever...
                return; // Might want to throw an error...
            // Send to NBu
            NBu.SendMessage(message);
        }
    }

    public class Neighbor {
        public Port Port { get; protected set; }
        TcpClient client;
        StreamWriter writer;

        public Neighbor(Port port, TcpClient client) {
            this.client = client;
            this.Port = port;
            if (client != null) {
                writer = new StreamWriter(client.GetStream());
                writer.AutoFlush = true;
            }
        }

        public void SendMessage(string message) {
            message.Trim();
            writer.WriteLine(message);
        }

    }
}
