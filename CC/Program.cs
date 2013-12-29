/* Ian Zunderdorp (3643034) & Bas Brouwer (3966747)
 * 
 */


using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Port = System.Int16;

namespace CC {
    
    class NetwProg {
        #region Console Window code
        const int SWP_NOSIZE = 0x0001; //Ignores the resize parameters when calling SetWindowPos.
        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        private static IntPtr MyConsole = GetConsoleWindow();

        [DllImport("user32.dll", EntryPoint = "SetWindowPos")]
        public static extern IntPtr SetWindowPos(IntPtr hWnd, int hWndInsertAfter, int x, int Y, int cx, int cy, int wFlags);
        #endregion
        static void Main(string[] args) {
#if DEBUG
            Console.WriteLine("Debugging mode");
#endif
            if (args.Length == 0) {
                args = new string[] { "1002", "1001" };
            }
            int iterator = 0; // For easier handling of the args array, since we have two optional parameters but we always know when they can appear
            if (args[0][0] == 'p') {
                // Set console position
                int x = int.Parse(args[iterator++].Substring(1)), y = int.Parse(args[iterator++].Substring(1));
                SetWindowPos(MyConsole, 0, x, y, 0, 0, SWP_NOSIZE);
                Console.Title = "port = {0}, x = {1}, y = {2}".Formatter(args[iterator], x, y); // Port number
            } // In both cases, set the proper requested console title
            else {
                Console.Title = "port = {0}".Formatter(args[iterator]); // Port number
            }
            // Initialize routing table
            Global.LocalPort = Port.Parse(args[iterator]);
            var local = new Neighbor(Global.LocalPort, null);
            Global.RoutingTable.Add(Global.LocalPort, new Row() { NBu = Global.LocalPort, Du = 0 });
            // Start listener service first
            Thread listener = new Thread(() => ListenAt(Global.LocalPort));
            listener.Start();

            // Connect to other ports
            while (++iterator < args.Length) {
                Port port = Port.Parse(args[iterator]);
#if DEBUG
                Console.WriteLine(port);
#endif
                if (port > Global.LocalPort) ConnectTo(port);
#if DEBUG
                else Console.WriteLine("Skipped");
#endif
            }
#if DEBUG
            Console.WriteLine("Connected to them all");
#endif


            // Input handling

            while (true) {
                var input = Console.ReadLine();
                if (input.StartsWith("S")) {
                    var prev = Global.Slowdown;
                    if(int.TryParse(input.Substring(2), out Global.Slowdown)) {
                        if (Global.Slowdown < 0) {
                            Global.Slowdown = prev;
                            Console.WriteLine(Global.Strings.ParameterError, "slowdown", "n", "a positive number");
                        }
                    }
                    Console.WriteLine(Global.Strings.ParameterError, "slowdown", "n", "a positive number");
                    continue;
                }
                if (input.StartsWith("R")) {
                    PrintRoutingTable();
                    continue;
                }
                if (input.StartsWith("M")) {
                    Console.WriteLine("Total number of distance estimates sent by this node: {0}".Formatter(Global.DistanceEstimates));
                    continue;
                }
                if (input.StartsWith("T")) {
                    input = input.Substring(2).Trim();
                    if (input.Equals("off", StringComparison.InvariantCultureIgnoreCase))
                        Global.Verbose = false;
                    else if (input.Equals("on", StringComparison.InvariantCultureIgnoreCase))
                        Global.Verbose = true;
                    else
                        Console.WriteLine(Global.Strings.ParameterError, "toggle", "state", "on or off");
                    continue;
                }
                if (input.StartsWith("D")) {
                    Port target;
                    if (Port.TryParse(input.Split(' ')[1], out target)) {
                        lock (Global.RoutingTable)
                            lock (Global.Neighbors)
                                if (Global.Neighbors.ContainsKey(target)) {
                                    Global.RoutingTable[target].SendMessage(Global.CreatePackage(Global.PackageNames.Disconnect, target, Global.LocalPort.ToString()));
                                    Disconnect(target);
                                }
                                else
                                    Console.WriteLine(Global.Strings.ParameterError, "delete", "port", "a valid port number that is connected to this node");
                    }
                    else
                        Console.WriteLine(Global.Strings.ParameterError, "delete", "port", "a valid port number");
                    continue;
                }
                if (input.StartsWith("C")) {
                    Port target;
                    if (Port.TryParse(input.Split(' ')[1], out target)) {
                        ConnectTo(target);
                    }
                    else
                        Console.WriteLine(Global.Strings.ParameterError, "connect", "port", "a valid port number");
                    
                    continue;
                }
                if (input.StartsWith("B")) {
                    var split = input.Split(' ');
                    Port target;
                    if (split.Length > 2 && Port.TryParse(split[1], out target)) {
                        if (!IsInPartition(target) || target == Global.LocalPort) {
                            Console.WriteLine(Global.Strings.ParameterError, "broadcast", "port", "a valid port number that is connected to the current node");
                            continue;
                        }
                        var message = new StringBuilder(split[2]);
                        for (int i = 3; i < split.Length; i++)
                            message.AppendFormat(" {0}", split[i]);
                        var msg = Global.CreatePackage("Broadcast", target, message.ToString());
                        Port sendTo;
                        lock (Global.RoutingTable)
                            sendTo = Global.RoutingTable[target].NBu;
#if DEBUG
                        Console.WriteLine("About to broadcast {0} to port {1} via port {2}", msg, target, sendTo);
#endif
                        lock (Global.Neighbors)
                            Global.Neighbors[sendTo].SendMessage(msg);
#if DEBUG
                        Console.WriteLine("Sent message to {0}", target);
#endif
                        continue;
                    }
                    if (split.Length > 2)
                        Console.WriteLine(Global.Strings.ParameterError, "broadcast", "port", "a valid port number");
                    else
                        Console.WriteLine(Global.Strings.ParameterError, "broadcast", "message", "a valid message");
                    continue;
                }
                Console.WriteLine("You entered an invalid command. Please retry");
            }
        }
        static void ListenAt(Port port) {
#if DEBUG
            Console.WriteLine("Listening at {0}".Formatter(port)); 
#endif
            TcpListener listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            while (true) {
                // Receive client connections and process them
#if DEBUG
                Console.WriteLine("Waiting for connection"); 
#endif
                var client = listener.AcceptTcpClient();
#if DEBUG
                Console.WriteLine("Received incoming connection"); 
#endif
                var reader = new StreamReader(client.GetStream());
                var msg = reader.ReadLine();
                var package = Global.UnpackPackage(msg);
                while (package[0] != Global.PackageNames.Connection) { // Error handling
#if DEBUG
                    Console.WriteLine("Error: Unexpected package received, dropping package.");
                    Console.WriteLine("Package: {0}", msg);
#endif
                    package = Global.UnpackPackage(reader.ReadLine());
                }
#if DEBUG
                Console.WriteLine("Processing connection");
#endif
                var newPort = Port.Parse(package[2].Substring(Global.Strings.ConnectionMessage.Length));
                ProcessClient(newPort, client); // Need to acquire proper port number.
                RoutingTable.UpdateDirtyPorts();
                RoutingTable.SendRoutingTableTo(newPort);

            }
        }

        static void ListenTo(Port port, TcpClient client) {
            try {
                using (StreamReader reader = new StreamReader(client.GetStream())) {
                    while (true) {
                        var message = reader.ReadLine();
#if DEBUG
                        Console.WriteLine(message);
#endif
                        Thread.Sleep(Global.Slowdown); // Sleep if required

                        if (!message.IsValidPackage()) {
                            // Error handling, not a valid package, would cause exceptions.
                            continue;
                        }
                        var package = Global.UnpackPackage(message);
                        Port target = Port.Parse(package[1]);
                        if (target == Global.LocalPort) {
                            // Handle package here
                            if (package[0] == Global.PackageNames.Broadcast)
                                Console.WriteLine(package[2]);
                            else if (package[0] == Global.PackageNames.Disconnect)
                                Disconnect(Port.Parse(package[2]));
                            else if (package[0] == Global.PackageNames.RoutingTableUpdate) {
                                try {
                                    package = package[2].Split('!');
                                    Port u = Port.Parse(package[0]);
                                    Port v = Port.Parse(package[1]);
                                    int NDISuv = int.Parse(package[2]);
                                    lock (Global.RoutingTable) {
                                        Global.RoutingTable[u].NDISu[v] = NDISuv;
                                        if (!Global.RoutingTable.ContainsKey(v))
                                            Global.RoutingTable.Add(v, new Row() { NBu = u, Du = NDISuv + Global.RoutingTable[u].Du });
                                        if (Global.RoutingTable[v].NDISu.ContainsKey(u))
                                            Global.RoutingTable[v].NDISu[u] = NDISuv;
                                        else
                                            Global.RoutingTable[v].NDISu.Add(u, NDISuv);
                                    }
                                    RoutingTable.Update(u, v);
                                }
                                catch { }
                            }

                        }
                        else {
                            lock(Global.RoutingTable)
                                if (Global.RoutingTable.ContainsKey(target)) {
                                    Global.RoutingTable[target].SendMessage(message);
                                    Console.WriteLine("Bericht voor {0} verzonden naar {1}".Formatter(target, Global.RoutingTable[target].NBu));
                                }
                                else {
                                    // Can't deliver package
#if DEBUG
                                    Console.WriteLine("Error: Package for {0} can't be delivered. Package info: {1}".Formatter(target, message));
#endif
                                }
                        }
                    }
                }
            }
            catch {
                Disconnect(port);
            }
        }

        public static void Disconnect(Port p) {
            Neighbor node;
            lock (Global.Neighbors)
                node = Global.Neighbors[p];
            if (node.Port == p) {
                node.Client.Close();
            }
            lock (Global.RoutingTable) {
                Global.RoutingTable[p].NDISu.Remove(Global.LocalPort);
                RoutingTable.Update(p);
            }
            if (Global.Verbose)
                Console.WriteLine("Verbinding verbroken met node {0}".Formatter(p));
            Thread th;
            lock (Global.Threads) {
                th = Global.Threads[p];
                Global.Threads.Remove(p);
            }
            th.Abort();
        }

        static bool IsInPartition(Port port) {
            return Global.RoutingTable.ContainsKey(port) && Global.RoutingTable[port].Du < Global.MaxDistance;
        }

        static void OnRoutingChange() {
            // Send update to all connected clients
        }

        static void ConnectTo(Port port) {
            // Connect to port
            try {
#if DEBUG
                Console.WriteLine("Starting attempt");
#endif
                var client = new TcpClient("localhost", port);
#if DEBUG
                Console.WriteLine("Connected");
#endif
                ProcessClient(port, client);
#if DEBUG
                Console.WriteLine("Processed");
#endif
                lock (Global.Neighbors)
                    Global.Neighbors[port].SendMessage(Global.CreatePackage(Global.PackageNames.Connection, port, "{0}{1}".Formatter(Global.Strings.ConnectionMessage, Global.LocalPort)));
#if DEBUG
                Console.WriteLine("Handshaken");
#endif
                RoutingTable.UpdateDirtyPorts();
                RoutingTable.SendRoutingTableTo(port);
            } // 
            catch (Exception e) {
#if DEBUG
                Console.WriteLine("Exception: {0}. \nSleep", e); 
#endif
                Thread.Sleep(10); ConnectTo(port); 
            }
        }

        private static void ProcessClient(Port port, TcpClient client) {
            Console.WriteLine("Processing port {0}.", port);
            var nb = new Neighbor(port, client);
            while (Global.RoutingTable == null) { Thread.Sleep(10); Console.WriteLine("Sleep"); }
            while (Global.Threads == null) { Thread.Sleep(10); Console.WriteLine("Sleep"); }
            while (Global.Neighbors == null) { Thread.Sleep(10); Console.WriteLine("Sleep"); }
            lock (Global.Neighbors) {
                if(Global.Neighbors.ContainsKey(port))
                    Global.Neighbors[port] = nb; 
                else Global.Neighbors.Add(port, nb); 
                Console.WriteLine("Neighbor added"); 
            }
            lock (Global.RoutingTable) {
                if (!Global.RoutingTable.ContainsKey(port))
                    Global.RoutingTable.Add(port, new Row() { NBu = port, Du = 1 });
                else {
                    Global.RoutingTable[port].NBu = port;
                    Global.RoutingTable[port].Du = 1;
                    Console.WriteLine("Routing Table row updated");
                }
                if(Global.RoutingTable[port].NDISu.ContainsKey(port))
                    Global.RoutingTable[port].NDISu[port] = 0; 
                else
                    Global.RoutingTable[port].NDISu.Add(port, 0); 
            }
            lock (Global.Threads) {
                var listenForMessages = new Thread(() => ListenTo(port, client));
                Global.Threads.Add(port, listenForMessages);
                listenForMessages.Start();
                Console.WriteLine("Listener added");
            }

            if (Global.Verbose)
                Console.WriteLine("Nieuwe verbinding met node {0}".Formatter(port));

            RoutingTable.AddDirty(port);
        }

        static void PrintRoutingTable() {
            lock (Global.RoutingTable) {
                int width = Global.RoutingTable.Max(x => x.Value.Du).ToString().Length;
                string format = "{0," + width.ToString() + ":" + new String('#', width - 1) + "0}";
                string rowSeparator = "+-----+{0}+-----+".Formatter(new String('-', width));
                Console.WriteLine("Routing Table");
                Console.WriteLine(rowSeparator);
                Console.WriteLine("|Node |D{0}|   Nb|", width > 0 ? new String(' ', width - 1) : "");
                Console.WriteLine(rowSeparator);
                foreach (var row in Global.RoutingTable) {
                    Console.WriteLine("|{0}|{1}|{2}|",
                        "{0,5:#####}".Formatter(row.Key),
                        format.Formatter(row.Value.Du),
                        "{0,5:#####}".Formatter(row.Value.NBu == Global.LocalPort ? "local" : row.Value.NBu.ToString()));
                }
                Console.WriteLine(rowSeparator);
            }
        }

        static void BroadcastRoutingTableChanges() {
            //
        }
    }
}