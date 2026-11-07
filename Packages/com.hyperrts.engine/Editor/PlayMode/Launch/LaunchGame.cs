using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using Unity.Scripting.LifecycleManagement;

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
    /// The game's half of the Play page launcher: what it can start and how. Subclass it once (like a <c>HubPage</c>) and
    /// the launcher, the main toolbar and Play with a choice use it.
    /// </summary>
    public abstract class LaunchGame
    {
        [NoAutoStaticsCleanup]
        private static LaunchGame _found;

        /// <summary>The game's launch, or null when no assembly subclasses it.</summary>
        public static LaunchGame Find() => _found ??= TypeDiscovery.Instances<LaunchGame>().FirstOrDefault();

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
    }
}
