using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Which street pipe sections stay: clear runs join across short gaps (driveways, roads) with an arch, and chains
    /// shorter than the kit's minimum are dropped, so no lone sections.
    /// </summary>
    public static class MapPipeRuns
    {
        /// <summary>The sections to lay; gaps to arch over (first index, count) go into <paramref name="arches"/>.</summary>
        public static bool[] Keep(IReadOnlyList<bool> pass, int minRun, int maxGap, List<(int First, int Count)> arches)
        {
            var kept = new bool[pass.Count];
            var chain = new List<(int First, int Count)>();
            foreach (var run in Runs(pass))
            {
                var end = chain.Count == 0 ? 0 : chain[^1].First + chain[^1].Count;
                if (chain.Count > 0 && run.First - end > maxGap)
                {
                    Close(chain, minRun, kept, arches);
                    chain.Clear();
                }

                chain.Add(run);
            }

            Close(chain, minRun, kept, arches);
            return kept;
        }

        /// <summary>Keeps a chain long enough, with an arch over each gap inside it.</summary>
        private static void Close(List<(int First, int Count)> chain, int minRun, bool[] kept,
            List<(int First, int Count)> arches)
        {
            var total = chain.Sum(run => run.Count);
            if (total < minRun)
            {
                return;
            }

            for (var i = 0; i < chain.Count; i++)
            {
                var (first, count) = chain[i];
                for (var j = first; j < first + count; j++)
                {
                    kept[j] = true;
                }

                if (i > 0)
                {
                    var gapStart = chain[i - 1].First + chain[i - 1].Count;
                    arches.Add((gapStart, first - gapStart));
                }
            }
        }

        /// <summary>The stretches of passing sections, as first index and count.</summary>
        private static IEnumerable<(int First, int Count)> Runs(IReadOnlyList<bool> pass)
        {
            var first = -1;
            for (var i = 0; i <= pass.Count; i++)
            {
                var passes = i < pass.Count && pass[i];
                if (passes && first < 0)
                {
                    first = i;
                }

                if (!passes && first >= 0)
                {
                    yield return (first, i - first);
                    first = -1;
                }
            }
        }

        /// <summary>The stretch along its length that makes an arch span a gap of <paramref name="gap"/> sections end to end.</summary>
        public static float ArchStretch(int gap, float section, float archLength) => gap * section / archLength;
    }
}
