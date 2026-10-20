using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// What a command-line server writes to <c>-report</c> when it quits: the match result and its cost, sampled each
    /// second of play (JSON through <c>JsonUtility</c>).
    /// </summary>
    [Serializable]
    public sealed class ServerReport
    {
        public const string Ended = "ended";
        public const string TimedOut = "timed-out";

        public string scene = "";

        /// <summary>UTC start of the server, ISO 8601.</summary>
        public string started = "";

        /// <summary><see cref="Ended"/> or <see cref="TimedOut"/>.</summary>
        public string outcome = "";
        public int exitCode;
        public float serverMinutes;

        /// <summary>Minutes since every slot had joined (or the join wait ran out).</summary>
        public float matchMinutes;

        /// <summary>0 for a draw or a match that didn't end.</summary>
        public int winningTeam;

        /// <summary>Errors and exceptions the server logged.</summary>
        public int errors;
        public List<PlayerResult> players = new();

        /// <summary>Seconds sampled, from the match start.</summary>
        public int samples;

        /// <summary>Process CPU time per wall second; 100 is one core busy.</summary>
        public Stat cpuPercentOfCore = new();

        /// <summary>Unity's "System Used Memory" profiler counter: the whole process, as the OS sees it.</summary>
        public Stat systemMemoryMB = new();

        /// <summary>Unity's "Total Used Memory" profiler counter.</summary>
        public Stat usedMemoryMB = new();
        public Stat connections = new();

        /// <summary>Everything the server's drivers sent, in kilobits per second.</summary>
        public Stat sentKbps = new();
        public Stat entities = new();

        [Serializable]
        public sealed class PlayerResult
        {
            public int slot;
            public int side;
            public int team;
            public int unitsBuilt;
            public int unitsLost;
            public int unitsKilled;
            public int buildingsBuilt;
            public int buildingsLost;
            public int buildingsDestroyed;
            public int resourcesGathered;

            /// <summary>Every player's result in <paramref name="entityManager"/>'s world, by slot.</summary>
            public static List<PlayerResult> Read(EntityManager entityManager)
            {
                using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<Player, PlayerSide, PlayerStats>()
                    .Build(entityManager);
                using var players = query.ToEntityArray(Allocator.Temp);
                using var relations = entityManager.CreateEntityQuery(ComponentType.ReadOnly<FactionRelations>());
                relations.TryGetSingleton(out FactionRelations teams);
                var results = new List<PlayerResult>(players.Length);
                foreach (var player in players)
                {
                    var faction = entityManager.GetComponentData<Player>(player).Faction;
                    var stats = entityManager.GetComponentData<PlayerStats>(player);
                    results.Add(new PlayerResult
                    {
                        slot = faction,
                        side = entityManager.GetComponentData<PlayerSide>(player).Value,
                        team = teams.GetTeam(faction),
                        unitsBuilt = stats.UnitsBuilt,
                        unitsLost = stats.UnitsLost,
                        unitsKilled = stats.UnitsKilled,
                        buildingsBuilt = stats.BuildingsBuilt,
                        buildingsLost = stats.BuildingsLost,
                        buildingsDestroyed = stats.BuildingsDestroyed,
                        resourcesGathered = stats.ResourcesGathered,
                    });
                }

                results.Sort((a, b) => a.slot.CompareTo(b.slot));
                return results;
            }
        }

        /// <summary>Mean, 95th percentile (nearest rank) and maximum of one sampled value.</summary>
        [Serializable]
        public sealed class Stat
        {
            public float mean;
            public float p95;
            public float max;

            /// <summary>All zero when nothing was sampled; <see cref="samples"/> says so.</summary>
            public static Stat Summarize(IReadOnlyList<float> values)
            {
                if (values.Count == 0)
                {
                    return new Stat();
                }

                var sorted = new List<float>(values);
                sorted.Sort();
                var sum = 0.0;
                foreach (var value in sorted)
                {
                    sum += value;
                }

                var rank = (int)Math.Ceiling(0.95 * sorted.Count) - 1;
                return new Stat { mean = (float)(sum / sorted.Count), p95 = sorted[rank], max = sorted[^1] };
            }
        }
    }
}
