using System;
using UnityEngine;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Starts a session from the command line: <c>-server</c>, <c>-host</c> or <c>-connect &lt;address&gt;</c>,
    /// with optional <c>-port &lt;n&gt;</c> and <c>-scene &lt;path&gt;</c> (default: reload the first scene).
    /// Dedicated server builds run with <c>-batchmode -nographics -server -scene &lt;map&gt;</c>.
    /// </summary>
    public static class NetworkLauncher
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Application.isEditor || NetworkSession.IsRunning)
            {
                return;
            }

            var args = Environment.GetCommandLineArgs();
            var port = ushort.TryParse(Value(args, "-port"), out var parsed) ? parsed : NetworkSession.DefaultPort;
            var scene = Value(args, "-scene") is { } path ? SessionScene.Load(path) : SessionScene.ReloadActive;
            if (Array.IndexOf(args, "-server") >= 0)
            {
                NetworkSession.StartServer(port, scene);
            }
            else if (Array.IndexOf(args, "-host") >= 0)
            {
                NetworkSession.StartHost(port, scene);
            }
            else if (Value(args, "-connect") is { } address)
            {
                NetworkSession.StartClient(address, port, scene);
            }
        }

        private static string Value(string[] args, string flag)
        {
            var index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
