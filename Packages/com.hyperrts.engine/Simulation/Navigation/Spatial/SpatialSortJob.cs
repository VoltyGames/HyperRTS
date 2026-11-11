using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Profiling;

namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>
    /// Counts the snapshots per cell, turns the counts into starts and files each snapshot in its cell, so a cell
    /// lists its entries in reverse query order every frame. Single-threaded: a thousand entries take microseconds.
    /// </summary>
    [BurstCompile]
    internal struct SpatialSortJob : IJob
    {
        private static readonly ProfilerMarker SortMarker = new("SpatialIndexSystem.Sort");

        public SpatialIndex Index;
        public NativeArray<SpatialEntry> Snapshots;

        public void Execute()
        {
            using var marker = SortMarker.Auto();
            var cells = Index.Grid.Count;
            var starts = Index.CellStarts;
            starts.Resize(cells + 1, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i <= cells; i++)
            {
                starts[i] = 0;
            }

            Index.VehicleCells.Resize(cells, NativeArrayOptions.UninitializedMemory);
            for (var i = 0; i < cells; i++)
            {
                Index.VehicleCells[i] = 0;
            }

            Index.Oversized.Clear();
            for (var i = 0; i < Snapshots.Length; i++)
            {
                var entry = Snapshots[i];
                if (entry.Entity == Entity.Null)
                {
                    continue;
                }

                if (entry.IsVehicle && entry.IsMoving)
                {
                    Index.MarkVehicleLane(entry);
                }

                if (entry.IsOversized(SpatialIndexSystem.CellSize))
                {
                    Index.Oversized.Add(entry);
                }
                else
                {
                    starts[Index.CellIndex(entry.Position) + 1]++;
                }
            }

            for (var i = 1; i <= cells; i++)
            {
                starts[i] += starts[i - 1];
            }

            Scatter(starts);
        }

        // Each cell's start serves as its write cursor, which leaves it at the next cell's start; shifting back
        // restores them. Slots are emptied as they go, ready for the next frame. Reverse order is the one the
        // head-on crowd scenarios settle in; the old hash order differed from run to run.
        private void Scatter(NativeList<int> starts)
        {
            var cells = Index.Grid.Count;
            Index.Entries.ResizeUninitialized(starts[cells]);
            for (var i = Snapshots.Length - 1; i >= 0; i--)
            {
                var entry = Snapshots[i];
                Snapshots[i] = default;
                if (entry.Entity == Entity.Null || entry.IsOversized(SpatialIndexSystem.CellSize))
                {
                    continue;
                }

                var cell = Index.CellIndex(entry.Position);
                Index.Entries[starts[cell]] = entry;
                starts[cell]++;
            }

            for (var i = cells; i > 0; i--)
            {
                starts[i] = starts[i - 1];
            }

            starts[0] = 0;
        }
    }
}
