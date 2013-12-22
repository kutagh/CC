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
        static Port LocalPort;
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
            LocalPort = Port.Parse(args[iterator]);
            var local = new Neighbor(LocalPort, null);
            Global.RoutingTable.Add(LocalPort, new Row() { NBu = local, Du = 0 });

            // Start listener service first
            Thread listener = new Thread(() => ListenAt(LocalPort));
            listener.Start();

            // Connect to other ports
            while (++iterator < args.Length) {
                Port port = Port.Parse(args[iterator]);
#if DEBUG
                Console.WriteLine(port);
#endif
                if (port > LocalPort) ConnectTo(port);
#if DEBUG
                else Console.WriteLine("Skipped");
#endif
            }
#if DEBUG
            Console.WriteLine("Connected to them all");
#endif
            foreach (var kvp in Global.RoutingTable) {
                if (kvp.Key != LocalPort) {
#if DEBUG
                    Console.WriteLine("Sending message to {0}".Formatter(kvp.Key));
#endif
                    kvp.Value.SendMessage(Global.CreatePackage(Global.PackageNames.Broadcast, kvp.Key, "Hello from {0}".Formatter(LocalPort)));
                }
            }

            // Input handling

            while (true) {
                var input = Console.ReadLine();
                if (input.StartsWith("S")) {

                    Console.WriteLine(Global.Strings.parameterError, "slowdown", "n", "a positive number");
                    continue;
                }
                if (input.StartsWith("R")) {
                    PrintRoutingTable();
                    continue;
                }
                if (input.StartsWith("M")) {
                    continue;
                }
                if (input.StartsWith("D")) {
                    Port target;
                    if (Port.TryParse(input.Split(' ')[1], out target)) {
                        if (Neighbors().Any(x => x.Port == target)) {
                            Global.RoutingTable[target].SendMessage(Global.CreatePackage(Global.PackageNames.Disconnect, target, LocalPort.ToString()));
                            Disconnect(target);
                        }
                        else
                            Console.WriteLine(Global.Strings.parameterError, "delete", "port", "a valid port number that is connected to this node");
                    }
                    else
                        Console.WriteLine(Global.Strings.parameterError, "delete", "port", "a valid port number");
                    continue;
                }
                if (input.StartsWith("C")) {
                    Port target;
                    if (Port.TryParse(input.Split(' ')[1], out target)) {
                        ConnectTo(target);
                    }
                    else
                        Console.WriteLine(Global.Strings.parameterError, "connect", "port", "a valid port number");
                    
                    continue;
                }
                if (input.StartsWith("B")) {
                    var split = input.Split(' ');
                    Port target;
                    if (split.Length > 2 && Port.TryParse(split[1], out target)) {
                        if (!IsInPartition(target) || target == LocalPort) {
                            Console.WriteLine(Global.Strings.parameterError, "broadcast", "port", "a valid port number that is connected to the current node");
                            continue;
                        }
                        var message = new StringBuilder(split[2]);
                        for (int i = 3; i < split.Length; i++)
                            message.AppendFormat(" {0}", split[i]);
                        var msg = Global.CreatePackage("Broadcast", target, message.ToString());
                        var sendTo = Global.RoutingTable[target].NBu;
#if DEBUG
                        Console.WriteLine("About to broadcast {0} to port {1} via port {2}", msg, target, sendTo);
#endif
                        sendTo.SendMessage(msg);
#if DEBUG
                        Console.WriteLine("Sent message to {0}", target);
#endif
                        continue;
                    }
                    if (split.Length > 2)
                        Console.WriteLine(Global.Strings.parameterError, "broadcast", "port", "a valid port number");
                    else
                        Console.WriteLine(Global.Strings.parameterError, "broadcast", "message", "a valid message");
                    continue;
                }
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
                var package = Global.UnpackPackage(reader.ReadLine());
                while (package[0] != Global.PackageNames.Connection) { // Error handling
#if DEBUG
                    Console.WriteLine("Error: Unexpected package received, dropping package."); 
#endif
                    package = Global.UnpackPackage(reader.ReadLine());
                }
                ProcessClient(Port.Parse(package[2].Substring(Global.Strings.ConnectionMessage.Length)), client); // Need to acquire proper port number.

            }
        }

        static void ListenTo(TcpClient client) {
            using (StreamReader reader = new StreamReader(client.GetStream())) {
                while (true) {
                    var message = reader.ReadLine();
#if DEBUG
                    Console.WriteLine(message);
#endif
                    if (!message.IsValidPackage()) {
                        // Error handling, not a valid package, would cause exceptions.
                        continue;
                    }
                    var package = Global.UnpackPackage(message);
                    Port target = Port.Parse(package[1]);
                    if (target == LocalPort) {
                        // Handle package here
                        if (package[0] == Global.PackageNames.Broadcast)
                            Console.WriteLine(package[2]);
                        else if (package[0] == Global.PackageNames.Disconnect)
                            Disconnect(Port.Parse(package[2]));
                        
                    }
                    else {
                        if (Global.RoutingTable.ContainsKey(target))
                            Global.RoutingTable[target].SendMessage(message);
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

        private static void Disconnect(Port p) {
            throw new NotImplementedException();
        }

        static bool IsInPartition(Port port) {
            return Global.RoutingTable.ContainsKey(port) && Global.RoutingTable[port].Du < Global.maxDistance;
        }

        static IEnumerable<Neighbor> Neighbors() {
            return Global.RoutingTable.Where(x => x.Value.Du == 1).Select(x => x.Value.NBu);
        }

        static void OnClientChange() {
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
                Global.RoutingTable[port].SendMessage(Global.CreatePackage(Global.PackageNames.Connection, port, "{0}{1}".Formatter(Global.Strings.ConnectionMessage, LocalPort)));
#if DEBUG
                Console.WriteLine("Handshaken");
#endif
            } // 
            catch {
#if DEBUG
                Console.WriteLine("Sleep"); 
#endif
                Thread.Sleep(10); ConnectTo(port); 
            }
        }

        private static void ProcessClient(Port port, TcpClient client) {
            var nb = new Neighbor(port, client);
            Global.RoutingTable.Add(port, new Row() { NBu = nb , Du = 1 });
            Global.RoutingTable[port].NDISu.Add(port, 0);

            Thread listenForMessages = new Thread(() => ListenTo(client));
            listenForMessages.Start();
            //throw new NotImplementedException();
        }

        static void PrintRoutingTable() {
            string rowSeparator = "+-----+-+-----+";
            Console.WriteLine("Routing Table");
            Console.WriteLine(rowSeparator);
            Console.WriteLine("|Node |D|   Nb|");
            Console.WriteLine(rowSeparator);
            foreach (var row in Global.RoutingTable) {
                Console.WriteLine("|{0}|{1}|{2}|", 
                    "{0,5:#####}".Formatter(row.Key), 
                    row.Value.Du, 
                    "{0,5:#####}".Formatter(row.Value.NBu.Port == LocalPort ? "local" : row.Value.NBu.Port.ToString()));
            }
            Console.WriteLine(rowSeparator);
        }

        static void BroadcastRoutingTableChanges() {
            //
        }
    }
}