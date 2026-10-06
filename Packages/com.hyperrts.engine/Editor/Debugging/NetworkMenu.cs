using HyperRTS.Network.Session;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>HyperRTS ▸ Network: turns the running Play-mode scene into a networked match.</summary>
    internal static class NetworkMenu
    {
        private const string Path = "HyperRTS/Network/";

        [MenuItem(Path + "Host", false, 30)]
        private static void Host() => NetworkSession.StartHost();

        [MenuItem(Path + "Join Localhost", false, 31)]
        private static void Join() => NetworkSession.StartClient("127.0.0.1");

        [MenuItem(Path + "Dedicated Server", false, 32)]
        private static void Server() => NetworkSession.StartServer();

        [MenuItem(Path + "Stop", false, 43)]
        private static void Stop() => NetworkSession.Stop();

        [MenuItem(Path + "Host", true)]
        [MenuItem(Path + "Join Localhost", true)]
        [MenuItem(Path + "Dedicated Server", true)]
        private static bool CanStart() => Application.isPlaying && !NetworkSession.IsRunning;

        [MenuItem(Path + "Stop", true)]
        private static bool CanStop() => Application.isPlaying && NetworkSession.IsRunning;
    }
}
