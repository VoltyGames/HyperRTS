using System;
using System.Globalization;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// A session start read from the command line by <see cref="NetworkLauncher"/>. Malformed or conflicting flags
    /// throw an <see cref="ArgumentException"/> naming the flag.
    /// </summary>
    public sealed class LaunchOptions
    {
        public LaunchMode Mode { get; private set; }
        public string Address { get; private set; }
        public ushort Port { get; private set; }

        /// <summary>The <c>-scene</c> path; null when not given.</summary>
        public string ScenePath { get; private set; }

        /// <summary>A client that watches instead of claiming a slot (<c>-observe</c>).</summary>
        public bool Observe { get; private set; }

        /// <summary>A server that quits once the match has ended (<c>-exit-on-end</c>).</summary>
        public bool ExitOnEnd { get; private set; }

        /// <summary>Server minutes after which the server quits whatever the match does; 0 for none.</summary>
        public float TimeLimitMinutes { get; private set; }

        /// <summary>Where a server writes its <see cref="ServerReport"/> when it quits; null for no report.</summary>
        public string ReportPath { get; private set; }

        /// <summary>A server that ends itself and so needs a <see cref="ServerMonitor"/>.</summary>
        public bool EndsItself => ExitOnEnd || TimeLimitMinutes > 0f;

        public static LaunchOptions Parse(string[] args)
        {
            var options = new LaunchOptions
            {
                Mode = ModeOf(args),
                Address = Value(args, "-connect"),
                Port = PortNumber(args),
                ScenePath = Value(args, "-scene"),
                Observe = Has(args, "-observe"),
                ExitOnEnd = Has(args, "-exit-on-end"),
                TimeLimitMinutes = TimeLimit(args),
                ReportPath = Value(args, "-report"),
            };
            options.Validate();
            return options;
        }

        private void Validate()
        {
            if (Observe && Mode != LaunchMode.Client)
            {
                throw new ArgumentException("-observe needs -connect.");
            }

            var serverFlags = EndsItself || ReportPath != null;
            if (serverFlags && Mode != LaunchMode.Server)
            {
                throw new ArgumentException("-exit-on-end, -time-limit and -report need -server.");
            }

            if (ReportPath != null && !EndsItself)
            {
                throw new ArgumentException("-report needs -exit-on-end or -time-limit, or the server never writes it.");
            }
        }

        private static LaunchMode ModeOf(string[] args)
        {
            var server = Has(args, "-server");
            var host = Has(args, "-host");
            var client = Has(args, "-connect");
            var count = (server ? 1 : 0) + (host ? 1 : 0) + (client ? 1 : 0);
            if (count > 1)
            {
                throw new ArgumentException("Pass only one of -server, -host and -connect.");
            }

            if (server)
            {
                return LaunchMode.Server;
            }

            if (host)
            {
                return LaunchMode.Host;
            }

            return client ? LaunchMode.Client : LaunchMode.None;
        }

        private static ushort PortNumber(string[] args)
        {
            var text = Value(args, "-port");
            if (text == null)
            {
                return NetworkSession.DefaultPort;
            }

            if (!ushort.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var port) || port == 0)
            {
                throw new ArgumentException($"-port needs a number from 1 to 65535, not '{text}'.");
            }

            return port;
        }

        private static float TimeLimit(string[] args)
        {
            var text = Value(args, "-time-limit");
            if (text == null)
            {
                return 0f;
            }

            var parsed = float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes);
            if (!parsed || minutes <= 0f)
            {
                throw new ArgumentException($"-time-limit needs a positive number of minutes, not '{text}'.");
            }

            return minutes;
        }

        private static bool Has(string[] args, string flag) => Array.IndexOf(args, flag) >= 0;

        private static string Value(string[] args, string flag)
        {
            var index = Array.IndexOf(args, flag);
            if (index < 0)
            {
                return null;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"{flag} needs a value.");
            }

            return args[index + 1];
        }
    }
}
