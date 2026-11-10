using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using Unity.Multiplayer.PlayMode;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>A map the launcher can start. <see cref="Slots"/> is how many players it seats.</summary>
    public readonly struct LaunchMap
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Slots;

        public LaunchMap(string id, string name, int slots)
        {
            Id = id;
            Name = name;
            Slots = slots;
        }
    }

    /// <summary>A side a slot can play. Id 0 is the random pick.</summary>
    public readonly struct LaunchSide
    {
        public readonly byte Id;
        public readonly string Name;

        public LaunchSide(byte id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    /// <summary>
    /// Starts a <see cref="LaunchChoice"/> in Play mode: the engine opens the game's menu scene and enters Play, and the
    /// game says what it can start and starts it. Subclass it once (like a <c>HubPage</c>) and the Play page launcher
    /// and the main toolbar use it.
    /// </summary>
    [InitializeOnLoad]
    public abstract class GameLauncher
    {
        [NoAutoStaticsCleanup]
        private static GameLauncher _found;

        [NoAutoStaticsCleanup]
        private static readonly PendingPlay Pending;

        private const double JoinWaitSeconds = 60;

        /// <summary>The Multiplayer Play Mode tag that makes an Editor instance join the launcher's match instead of idling.</summary>
        public const string JoinTag = "HyperRtsJoin";

        static GameLauncher()
        {
            Pending = new PendingPlay("HyperRTS.GameLauncher.Pending", Begin);
            EditorApplication.playModeStateChanged += JoinWhenTagged;
        }

        /// <summary>The game's launcher, or null when no assembly subclasses it.</summary>
        public static GameLauncher Find() => _found ??= TypeDiscovery.Instances<GameLauncher>().FirstOrDefault();

        /// <summary>Opens <see cref="MenuScene"/> and enters Play mode; a choice that enters the map is then handed to <see cref="Start"/>.</summary>
        public void Play(LaunchChoice choice)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(MenuScene);
            JoinInvite.Clear();
            Pending.Enter(choice.entry == LaunchEntry.Map ? JsonUtility.ToJson(choice) : "", choice.HostsVirtualPlayers);
        }

        public abstract IReadOnlyList<LaunchMap> Maps { get; }

        public bool TryFindMap(string id, out LaunchMap map)
        {
            foreach (var candidate in Maps)
            {
                if (candidate.Id == id)
                {
                    map = candidate;
                    return true;
                }
            }

            map = default;
            return false;
        }

        public abstract IReadOnlyList<LaunchSide> Sides { get; }

        /// <summary>The scene Play starts from, which loads the game's services.</summary>
        public abstract string MenuScene { get; }

        /// <summary>Starts the match in Play mode, once the game's services are up in <see cref="MenuScene"/>.</summary>
        public abstract void Start(LaunchChoice choice);

        /// <summary>
        /// Joins the match <see cref="Start"/> hosts, on <paramref name="address"/> and <paramref name="port"/>, once the
        /// game's services are up in a virtual player. <paramref name="details"/> is what <see cref="Invite"/> got.
        /// </summary>
        public abstract void Join(string address, ushort port, string details);

        /// <summary>Call it from <see cref="Start"/> once the host listens, so virtual players can join with <paramref name="details"/>.</summary>
        protected static void Invite(string details) => JoinInvite.ForThisMachine(details).Publish();

        private static void Begin(string json) => Find().Start(JsonUtility.FromJson<LaunchChoice>(json));

        private static void JoinWhenTagged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            var joins = !CurrentPlayer.IsMainEditor && CurrentPlayer.Tags.Contains(JoinTag);
            if (!joins)
            {
                return;
            }

            var until = EditorApplication.timeSinceStartup + JoinWaitSeconds;
            void Poll()
            {
                if (!EditorApplication.isPlaying)
                {
                    EditorApplication.update -= Poll;
                    return;
                }

                if (JoinInvite.TryRead(out var invite))
                {
                    EditorApplication.update -= Poll;
                    Find().Join(invite.address, invite.port, invite.details);
                }
                else if (EditorApplication.timeSinceStartup > until)
                {
                    EditorApplication.update -= Poll;
                    Debug.LogError("[HyperRTS] The host published no join invite: start the match from the launcher in the main Editor.");
                }
            }

            EditorApplication.update += Poll;
        }
    }
}
