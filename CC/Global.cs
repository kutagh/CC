using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Port = System.Int16;

namespace CC {
    public static class Global {
        public static class Strings {
            public static string ConnectionMessage = "Connection from ";
            public static string ParameterError = "The {0} parameter '{1}' was not correct, please enter {2}.";
            public static string RoutingTableChange = "{0}!{1}!{2}";
        }

        public static class PackageNames {
            public static string Connection = "Connection Handshake";
            public static string Disconnect = "Disconnect message";
            public static string Broadcast  = "Broadcast";
            public static string RoutingTableUpdate = "Routing Table Update Message";
        }
        public static Dictionary<Port, Row> RoutingTable = new Dictionary<Port, Row>();
        public static Dictionary<Port, Thread> Threads = new Dictionary<Port,Thread>();
        public static Dictionary<Port, Neighbor> Neighbors = new Dictionary<Port, Neighbor>();
        public static string Formatter(this string s, params object[] parameters) { return string.Format(s, parameters); }

        static char separator = '|';
        public static int MaxDistance = 50;
        public static int Slowdown = 0;
        public static int DistanceEstimates = 0;
        public static bool Verbose = false;
        public static Port LocalPort;

        public static string CreatePackage(string packageName, Port destination, string payload = "no payload") {
            return "{0}{3}{1}{3}{2}".Formatter(packageName, destination, payload, separator);
        }

        public static string[] UnpackPackage(string package) {
            if (package.IsValidPackage())
                return package.Split(separator);
            else return new string[] { package };
        }

        public static bool IsValidPackage(this string package) {
            return package.Count(x => x == separator) == 2;
        }

        public static KeyValuePair<T, T1> GetMinimum<T,T1>(this Dictionary<T, T1> dict) where T1 : IComparable<T1> {
            var best = dict.FirstOrDefault();
            foreach (var current in dict)
                if (current.Value.CompareTo(best.Value) < 0)
                    best = current;
            return best;
        }
    }
}
