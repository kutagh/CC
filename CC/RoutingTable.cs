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
            if (NBu == Global.LocalPort) { }
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
            lock (dirty)
                dirty.Add(port);
        }
        static HashSet<Port> dirty = new HashSet<Port>();


        public static void Update(params Port[] p) {
            lock (dirty)
                foreach (var port in p) dirty.Add(port);
            UpdateDirtyPorts();
        }
        public static void UpdateDirtyPorts() {
            lock (Global.RoutingTable)
                lock (Global.Neighbors)
                    lock (dirty) {
                        var announceChanges = new List<Port>();
                        foreach (var port in dirty) {
                            if (port == Global.LocalPort) continue;
                            if (!Global.RoutingTable.ContainsKey(port)) continue;
                            var row = Global.RoutingTable[port];
                            var oldDu = row.Du;
                            if (row.NDISu.Count > 0) {
                                var best = row.NDISu.GetMinimum();
                                row.NBu = best.Key;
                                row.Du = row.NBu == port ? 1 : best.Value + 1;
                            }
                            else {
                                // Unreachable node
                                row.Du = Global.MaxDistance;
                                row.NBu = 0;

                            }
                            if (row.Du != oldDu)
                                announceChanges.Add(port);
                        }
                        // Update everyone about the changes in announceChanges
                        foreach (var port in announceChanges) {
                            foreach (var kvp in Global.Neighbors)
                                kvp.Value.SendMessage(Global.CreatePackage(Global.PackageNames.RoutingTableUpdate, kvp.Key, Global.Strings.RoutingTableChange.Formatter(Global.LocalPort, port, Global.RoutingTable[port].Du)));
                        }

                        dirty.Clear();
                        announceChanges.Clear();
                    }
        }

        public static void SendRoutingTableTo(Port port) {
            lock (Global.RoutingTable)
                foreach (var kvp in Global.RoutingTable)
                    Global.Neighbors[port].SendMessage(Global.CreatePackage(Global.PackageNames.RoutingTableUpdate, kvp.Key, Global.Strings.RoutingTableChange.Formatter(Global.LocalPort, kvp.Key, kvp.Value.Du)));

        }

    }
}
