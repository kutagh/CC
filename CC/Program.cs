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
    public static class Globals {
        public static Dictionary<Port, Row> RoutingTable = new Dictionary<Port, Row>();

        public static string Formatter(this string s, params object[] parameters) { return string.Format(s, parameters); }

        public static string ConnectionMessage = "Connection from";
    }
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
                args = new string[] { "1000", "1001" };
            }
            int iterator = 0;
            if (args[0][0] == 'p') {
                // Set console position
                int x = int.Parse(args[iterator++].Substring(1)), y = int.Parse(args[iterator++].Substring(1));
                SetWindowPos(MyConsole, 0, x, y, 0, 0, SWP_NOSIZE);
                Console.Title = "port = {0}, x = {1}, y = {2}".Formatter(args[iterator], x, y); // Port number
            }
            else {
                Console.Title = "port = {0}".Formatter(args[iterator]); // Port number
            }
            // Main listener
            LocalPort = short.Parse(args[iterator++]);
            var local = new Neighbor(LocalPort, null);
            Globals.RoutingTable.Add(LocalPort, new Row() { NBu = local, Du = 0 });
            
            // Testing purposes
            Globals.RoutingTable.Add(45, new Row() { NBu = local, Du = 1 }); 
            PrintRoutingTable(LocalPort); 
            
            
            Thread listener = new Thread(() => ListenAt(LocalPort));
            listener.Start();
        }

        static void ListenAt(short port) {
            TcpListener listener = new TcpListener(System.Net.IPAddress.Any, port);
            listener.Start();
            while (true) {
                // Receive client connections and process them
                var client = listener.AcceptTcpClient();
                using (StreamReader reader = new StreamReader(client.GetStream())) {
                    var message = reader.ReadLine();
                    if (!message.StartsWith(Globals.ConnectionMessage)) {
                        List<string> messages = new List<string>();
                        do {
                            messages.Add(message);
                            message = reader.ReadLine();
                        } while (!message.StartsWith(Globals.ConnectionMessage));
                    }
                    ProcessClient(short.Parse(message.Substring(Globals.ConnectionMessage.Length)), client); // Need to acquire proper port number.
                }
            }
        }

        static void ListenTo(TcpClient client) {
            using (StreamReader writer = new StreamReader(client.GetStream())) {
                while (true) {
                    // Receive messages and parse them
                }
            }
        }

        static void OnClientChange() {
            // Send update to all connected clients
        }

        static void ConnectTo(short port) {
            // Connect to port
            try {
                var client = new TcpClient("localhost", port);
                ProcessClient(port, client);
                Globals.RoutingTable[port].SendMessage("{0}{1}".Formatter(Globals.ConnectionMessage, LocalPort));
            } // 
            catch { Thread.Sleep(10); ConnectTo(port); }
        }

        private static void ProcessClient(short port, TcpClient client) {
            var nb = new Neighbor(port, client);
            Globals.RoutingTable.Add(port, new Row() { NBu = nb });
            throw new NotImplementedException();
        }

        static void PrintRoutingTable(short localPort) {
            string rowSeparator = "+-----+-+-----+";
            Console.WriteLine("Routing Table");
            Console.WriteLine(rowSeparator);
            Console.WriteLine("|Node |D|   Nb|");
            Console.WriteLine(rowSeparator);
            foreach (var row in Globals.RoutingTable) {
                if (row.Key == localPort)
                    Console.WriteLine("|{0}|0|local|", "{0,5:#####}".Formatter(localPort));
                else
                    Console.WriteLine("|{0}|{1}|{2}|", "{0,5:#####}".Formatter(row.Key), row.Value.Du, "{0,5:#####}".Formatter(row.Value.NBu.Port));
            }
            Console.WriteLine(rowSeparator);
        }
    }

    public class Row {
        /// <summary>
        /// Distance from node u to node v
        /// </summary>
        public short Du;

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

        public Neighbor(short port, TcpClient client) {
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