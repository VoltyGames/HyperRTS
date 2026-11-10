using System;
using System.IO;
using HyperRTS.Network.Session;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>
    /// Where a launcher-hosted match can be joined, written by the host's Editor and read by the virtual players. A file in
    /// the user's temp folder, because virtual players are separate Editor processes with their own project folder.
    /// </summary>
    [Serializable]
    internal sealed class JoinInvite
    {
        public string address;
        public ushort port;

        /// <summary>What the game needs to join the same match, such as the resolved setup.</summary>
        public string details;

        private static string FilePath => Path.Combine(Path.GetTempPath(), "HyperRTS", "join-invite.json");

        /// <summary>The host's own machine and the port a host session listens on.</summary>
        public static JoinInvite ForThisMachine(string details) =>
            new() { address = "127.0.0.1", port = NetworkSession.DefaultPort, details = details };

        public static void Clear()
        {
            File.Delete(FilePath);
        }

        public void Publish()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonUtility.ToJson(this));
        }

        public static bool TryRead(out JoinInvite invite)
        {
            invite = null;
            if (!File.Exists(FilePath))
            {
                return false;
            }

            try
            {
                invite = JsonUtility.FromJson<JoinInvite>(File.ReadAllText(FilePath));
            }
            catch (IOException)
            {
                // The host is still writing it: try again on the next poll.
                return false;
            }

            return invite != null;
        }
    }
}
