using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Port = System.Int16;

namespace CC {
    public static class Global {
        public static class Strings {
            public static string ConnectionMessage = "Connection from";
            public static string parameterError = "The {0} parameter '{1}' was not correct, please enter {2}.";
        }

        public static class PackageNames {
            public static string Connection = "Connection Handshake";
            public static string Disconnect = "Disconnect message";
            public static string Broadcast  = "Broadcast";
        }
        public static Dictionary<Port, Row> RoutingTable = new Dictionary<Port, Row>();

        public static string Formatter(this string s, params object[] parameters) { return string.Format(s, parameters); }


        static char separator = '|';

        public static string CreatePackage(string packageName, Port destination, string payload) {
            return "{0}{3}{1}{3}{2}".Formatter(packageName, destination, payload, separator);
        }

        public static string[] UnpackPackage(string package) {
            if (package.Count(x => x == separator) == 2)
                return package.Split(separator);
            else return new string[] { package };
        }
    }
}
