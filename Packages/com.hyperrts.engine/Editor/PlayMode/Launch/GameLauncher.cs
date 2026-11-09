using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
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

        static GameLauncher() => Pending = new PendingPlay("HyperRTS.GameLauncher.Pending", Begin);

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
            Pending.Enter(choice.entry == LaunchEntry.Map ? JsonUtility.ToJson(choice) : "");
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

        private static void Begin(string json) => Find().Start(JsonUtility.FromJson<LaunchChoice>(json));
    }
}
