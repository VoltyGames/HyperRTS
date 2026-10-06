using HyperRTS.Network.Session;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>HyperRTS ▸ Network: turns the running Play-mode scene into a networked match.</summary>
    internal static class NetworkMenu
    {
        private const string Host = EditorMenu.Network + "Host";
        private const string Join = EditorMenu.Network + "Join Localhost";
        private const string Server = EditorMenu.Network + "Dedicated Server";
        private const string Stop = EditorMenu.Network + "Stop";

        [MenuItem(Host, false, EditorMenu.NetworkPriority)]
        private static void StartHost() => NetworkSession.StartHost();

        [MenuItem(Join, false, EditorMenu.NetworkPriority + 1)]
        private static void StartClient() => NetworkSession.StartClient("127.0.0.1");

        [MenuItem(Server, false, EditorMenu.NetworkPriority + 2)]
        private static void StartServer() => NetworkSession.StartServer();

        [MenuItem(Stop, false, EditorMenu.NetworkPriority + 2 + EditorMenu.SeparatorGap)]
        private static void StopSession() => NetworkSession.Stop();

        [MenuItem(Host, true)]
        [MenuItem(Join, true)]
        [MenuItem(Server, true)]
        private static bool CanStart() => Application.isPlaying && !NetworkSession.IsRunning;

        [MenuItem(Stop, true)]
        private static bool CanStop() => Application.isPlaying && NetworkSession.IsRunning;
    }
}
