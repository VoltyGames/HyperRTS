using System;
using System.Collections.Generic;

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
