using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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
            lock (Global.Neighbors)
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
            if (Port == Global.LocalPort) return;
            try {
#if DEBUG
                Console.WriteLine("Sending message: {0}", message); 
#endif
                message.Trim();
                writer.WriteLine(Global.Strings.SendingFrom, Global.LocalPort, message);
            }
            catch {
#if DEBUG
                Console.WriteLine("Connection lost"); 
#endif

                NetwProg.Disconnect(Port);
                //throw;
            }
        }

    }

    public static class RoutingTable {
        public static void AddDirty(Port port) {
            lock (dirty)
                dirty.Add(port);
        }
        static HashSet<Port> dirty = new HashSet<Port>();
        static HashSet<Port> newPorts = new HashSet<Port>();

        public static void Update(params Port[] p) {
            lock (dirty)
                foreach (var port in p) dirty.Add(port);
            UpdateDirtyPorts();
        }

        public static void AddNewPort(Port p) {
            lock (newPorts)
                newPorts.Add(p);
        }
        public static void UpdateDirtyPorts() {
            lock (Global.RoutingTable) lock (Global.Neighbors) lock (dirty) lock(newPorts) {
                        var announceChanges = new HashSet<Port>();
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
                            if (row.Du != oldDu) {
                                announceChanges.Add(port);
                                if (Global.Verbose) Console.WriteLine("Afstand naar {0} nu {1}", port, row.Du);
                            }
                        }
                        foreach (var p in newPorts)
                            announceChanges.Add(p);
                        // Update everyone about the changes in announceChanges
                        foreach (var port in announceChanges) {
                            foreach (var kvp in Global.Neighbors) {
                                kvp.Value.SendMessage(Global.CreatePackage(Global.PackageNames.RoutingTableUpdate, kvp.Key, Global.Strings.RoutingTableChange.Formatter(Global.LocalPort, port, Global.RoutingTable[port].Du)));
                                if (Global.Verbose) Console.WriteLine("Schatting verstuurd naar {0}: Afstand naar {1} is {2} via {3}", kvp.Key, port, Global.RoutingTable[port].Du, Global.RoutingTable[port].NBu);
                                Interlocked.Increment(ref Global.DistanceEstimates);
                            }
                        }

                        dirty.Clear();
                        newPorts.Clear();
                    }
        }

        public static void SendRoutingTableTo(Port port) {
            lock (Global.RoutingTable)
                foreach (var kvp in Global.RoutingTable) {
                    Global.Neighbors[port].SendMessage(Global.CreatePackage(Global.PackageNames.RoutingTableUpdate, port, Global.Strings.RoutingTableChange.Formatter(Global.LocalPort, kvp.Key, kvp.Value.Du)));
                    if(Global.Verbose) Console.WriteLine("Schatting verstuurd naar {0}: Afstand naar {1} is {2} via {3}", port, kvp.Key, kvp.Value.Du, kvp.Value.NBu);
                    Interlocked.Increment(ref Global.DistanceEstimates);
                }

        }

        public static void BroadcastRoutingTable() {
            lock (Global.RoutingTable) lock (Global.Neighbors) {
                foreach (var kvp in Global.RoutingTable)
                    foreach (var nb in Global.Neighbors) {
                        nb.Value.SendMessage(Global.CreatePackage(Global.PackageNames.RoutingTableUpdate, nb.Key, Global.Strings.RoutingTableChange.Formatter(Global.LocalPort, kvp.Key, kvp.Value.Du)));
                        if (Global.Verbose) Console.WriteLine("Schatting verstuurd naar {0}: Afstand naar {1} is {2} via {3}", nb.Key, kvp.Key, kvp.Value.Du, kvp.Value.NBu);
                        Interlocked.Increment(ref Global.DistanceEstimates);
                    }
            }
        }

    }
}
