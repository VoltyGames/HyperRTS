using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;

namespace HyperRTS.Simulation.Vision
{
    /// <summary>
    /// Rebuilds one band of <see cref="BandRows"/> grid rows of every team's visible, tall and close planes from the
    /// viewers' <see cref="SightArea"/>s, a word at a time. Bands run in parallel: each writes only its own rows.
    /// </summary>
    [BurstCompile]
    internal struct FogMergeJob : IJobParallelFor
    {
        public const int BandRows = 8;

        private static readonly ProfilerMarker MergeMarker = new("FogMergeJob.Merge");

        [NativeDisableParallelForRestriction] public FogOfWar Fog;
        [ReadOnly] public NativeArray<Entity> Viewers;
        [ReadOnly] public ComponentLookup<SightArea> Areas;
        [ReadOnly] public BufferLookup<SightAreaBits> Bits;

        public static int Bands(in FogOfWar fog) => (fog.Grid.Size.y + BandRows - 1) / BandRows;

        public void Execute(int band)
        {
            using var marker = MergeMarker.Auto();
            var low = band * BandRows;
            var high = math.min(low + BandRows, Fog.Grid.Size.y);
            Clear(Fog.Visible, low, high);
            Clear(Fog.VisibleTall, low, high);
            Clear(Fog.Close, low, high);
            foreach (var viewer in Viewers)
            {
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
                    Or(Fog.Visible, area, words, SightArea.Ground, y);
                    Or(Fog.VisibleTall, area, words, SightArea.Tall, y);
                    Or(Fog.Close, area, words, SightArea.Close, y);
                }
            }
        }

        private readonly void Or(NativeArray<ulong> plane, in SightArea area, NativeArray<ulong> words, int layer, int y)
        {
            var row = y - area.Origin.y;
            for (var word = 0; word < area.RowWords; word++)
            {
                Fog.Or(plane, area.Team, y, area.Origin.x + word * 64, words[area.Word(layer, row, word)]);
            }
        }

        private readonly void Clear(NativeArray<ulong> plane, int low, int high)
        {
            for (var team = 0; team < FactionRelations.MaxTeams; team++)
            {
                var start = team * Fog.PlaneWords;
                for (var i = low * Fog.RowWords; i < high * Fog.RowWords; i++)
                {
                    plane[start + i] = 0ul;
                }
            }
        }
    }
}
