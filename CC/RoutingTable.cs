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
        public int Du = Global.MaxDistance;

        /// <summary>
        /// Preferred neighbor w of node u to reach node v
        /// </summary>
        public Port NBu = 0;

        /// <summary>
        /// Dictionary that contains all distances of other nodes to node v, as known by node u
        /// </summary>
        public Dictionary<Port, int> NDISu = new Dictionary<Port, int>();

        public void SendMessage(string message) {
            if (NBu == 0) {// Can't send whatsoever...
                if (NDISu.Count() == 0)
                    return; // Might want to throw an error...
                else { 
                    // Fix it
                    throw new NotImplementedException();
                }
            }
            if(NBu == Global.LocalPort)
            // Send to NBu
            Global.Neighbors[NBu].SendMessage(message);
        }
    }

    public class Neighbor {
        public Port Port { get; protected set; }
        public TcpClient Client { get; protected set; }
        StreamWriter writer;

        public Neighbor(Port port, TcpClient client) {
            this.Client = client;
            this.Port = port;
            if (client != null) {
                writer = new StreamWriter(client.GetStream());
                writer.AutoFlush = true;
            }
        }

        public void SendMessage(string message) {
            try {
                message.Trim();
                writer.WriteLine(message);
            }
            catch {
                NetwProg.Disconnect(Port);
                throw;
            }
        }

    }

    public static class RoutingTable {
        public static void AddDirty(Port port) {
            dirty.Add(port);
        }
        static List<Port> dirty = new List<Port>();

        public static void Update() {
            foreach (var port in dirty) {
                if (port == Global.LocalPort) continue;
                var row = Global.RoutingTable[port];
                if (row.NDISu.Count > 0) {
                    var best = row.NDISu.GetMinimum();
                    row.NBu = best.Key;
                    row.Du = best.Value + 1;
                }
                else {
                    // Unreachable node
                }
            }

            // Update everyone

            dirty.Clear();
        }

    }
}
