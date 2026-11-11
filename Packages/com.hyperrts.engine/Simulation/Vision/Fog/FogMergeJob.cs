using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>
    /// Rebuilds a band of <see cref="BandRows"/> plane rows of every team's visible, tall and close planes from the
    /// viewers' <see cref="SightArea"/>s, a word at a time. The planes lay every team's rows end to end, so a batch is
    /// a run of whole rows and writes only inside its own range of the three planes.
    /// </summary>
    [BurstCompile]
    internal struct FogMergeJob : IJobParallelForBatch
    {
        public const int BandRows = 8;

        private static readonly ProfilerMarker MergeMarker = new("FogMergeJob.Merge");

        public NativeArray<ulong> Visible;
        public NativeArray<ulong> VisibleTall;
        public NativeArray<ulong> Close;
        public int Width;
        public int Rows;
        public int RowWords;

        /// <summary>Viewers grouped by team, team <c>t</c> from <see cref="TeamStarts"/>[t] up to [t + 1].</summary>
        [ReadOnly] public NativeArray<Entity> Viewers;
        [ReadOnly] public NativeArray<int> TeamStarts;
        [ReadOnly] public ComponentLookup<SightArea> Areas;
        [ReadOnly] public BufferLookup<SightAreaBits> Bits;

        public static int BatchWords(in FogOfWar fog) => BandRows * fog.RowWords;

        public void Execute(int startIndex, int count)
        {
            using var marker = MergeMarker.Auto();
            Clear(Visible, startIndex, count);
            Clear(VisibleTall, startIndex, count);
            Clear(Close, startIndex, count);
            var end = (startIndex + count) / RowWords;
            for (var row = startIndex / RowWords; row < end;)
            {
                var team = row / Rows;
                var last = math.min(end, (team + 1) * Rows);
                MergeTeam(team, row - team * Rows, last - team * Rows);
                row = last;
            }
        }

        private void MergeTeam(int team, int low, int high)
        {
            for (var i = TeamStarts[team]; i < TeamStarts[team + 1]; i++)
            {
                var viewer = Viewers[i];
                var area = Areas[viewer];
                var first = math.max(low, area.Origin.y);
                var last = math.min(high, area.Origin.y + area.Width);
                if (area.Radius < 0 || first >= last)
                {
                    continue;
                }

                var words = Bits[viewer].Reinterpret<ulong>().AsNativeArray();
                for (var y = first; y < last; y++)
                {
                    Or(Visible, area, words, SightArea.Ground, y);
                    Or(VisibleTall, area, words, SightArea.Tall, y);
                    Or(Close, area, words, SightArea.Close, y);
                }
            }
        }

        private void Or(NativeArray<ulong> plane, in SightArea area, NativeArray<ulong> words, int layer, int y)
        {
            var row = y - area.Origin.y;
            for (var word = 0; word < area.RowWords; word++)
            {
                OrBits(plane, area.Team, y, area.Origin.x + word * 64, words[area.Word(layer, row, word)]);
            }
        }

        // ORs 64 cells' bits starting at x into a row, dropping those off the grid.
        private void OrBits(NativeArray<ulong> plane, byte team, int row, int x, ulong bits)
        {
            if (x < 0)
            {
                bits = x <= -64 ? 0ul : bits >> -x;
                x = 0;
            }

            var room = Width - x;
            if (bits == 0ul || room <= 0)
            {
                return;
            }

            bits &= room >= 64 ? ulong.MaxValue : (1ul << room) - 1ul;
            var word = (team * Rows + row) * RowWords + (x >> 6);
            var shift = x & 63;
            plane[word] |= bits << shift;
            if (shift != 0 && (x >> 6) + 1 < RowWords)
            {
                plane[word + 1] |= bits >> (64 - shift);
            }
        }

        private static void Clear(NativeArray<ulong> plane, int start, int count)
        {
            for (var i = start; i < start + count; i++)
            {
                plane[i] = 0ul;
            }
        }
    }
}
