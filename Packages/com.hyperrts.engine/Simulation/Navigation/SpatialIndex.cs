using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Singleton uniform-grid hash of living units and buildings, with blockers in their own grid, rebuilt each frame by
    /// <see cref="SpatialIndexSystem"/>. Read it with <c>SystemAPI.GetSingleton</c> so job dependencies chain.
    /// </summary>
    public struct SpatialIndex : IComponentData
    {
        public NativeParallelMultiHashMap<int, SpatialEntry> Cells;
        public float CellSize;

        /// <summary>
        /// Entries wider than a cell (airfields, shipyards). Cells hold the rest by centre, so queries pad by one cell
        /// and scan this short list instead of padding every query by the widest building.
        /// </summary>
        public NativeList<SpatialEntry> Oversized;

        /// <summary>Enabled <see cref="Blocker"/>s, kept apart so only movement ever sees them.</summary>
        public NativeParallelMultiHashMap<int, SpatialEntry> Blockers;

        /// <summary>Blockers wider than a cell, as <see cref="Oversized"/> is for the rest.</summary>
        public NativeList<SpatialEntry> WideBlockers;

        public readonly int2 CellAt(float3 position) => CellAt(position, CellSize);

        public static int2 CellAt(float3 position, float cellSize) => (int2)math.floor(position.xz / cellSize);

        /// <summary>Unique for cells within ±32k of the origin, so queries never see foreign cells twice.</summary>
        public static int Key(int2 cell) => (cell.x & 0xFFFF) | (cell.y << 16);

        public static int2 CellOfKey(int key) => new((short)(key & 0xFFFF), key >> 16);

        /// <summary>Visits entries whose circle overlaps the query circle on the XZ plane.</summary>
        public readonly void Query<T>(float3 center, float radius, ref T visitor) where T : struct, ISpatialVisitor =>
            Query(Cells, Oversized, center, radius, false, ref visitor);

        /// <summary>Visits entries whose <see cref="SpatialEntry.HullReach"/> overlaps the query circle.</summary>
        public readonly void QueryHulls<T>(float3 center, float radius, ref T visitor)
            where T : struct, ISpatialVisitor =>
            Query(Cells, Oversized, center, radius, true, ref visitor);

        /// <summary>Visits blockers whose <see cref="SpatialEntry.HullReach"/> overlaps the query circle.</summary>
        public readonly void QueryBlockers<T>(float3 center, float radius, ref T visitor)
            where T : struct, ISpatialVisitor =>
            Query(Blockers, WideBlockers, center, radius, true, ref visitor);

        private readonly void Query<T>(NativeParallelMultiHashMap<int, SpatialEntry> cells,
            NativeList<SpatialEntry> oversized, float3 center, float radius, bool hulls, ref T visitor)
            where T : struct, ISpatialVisitor
        {
            var search = radius + CellSize;
            var min = CellAt(center - search);
            var max = CellAt(center + search);

            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!cells.TryGetFirstValue(Key(new int2(x, y)), out var entry, out var iterator))
                    {
                        continue;
                    }

                    do
                    {
                        VisitIfOverlapping(entry, center, radius, hulls, ref visitor);
                    } while (cells.TryGetNextValue(out entry, ref iterator));
                }
            }

            foreach (var entry in oversized)
            {
                VisitIfOverlapping(entry, center, radius, hulls, ref visitor);
            }
        }

        private static void VisitIfOverlapping<T>(in SpatialEntry entry, float3 center, float radius, bool hulls,
            ref T visitor) where T : struct, ISpatialVisitor
        {
            var reach = radius + (hulls ? entry.HullReach : entry.Radius);
            if (math.distancesq(entry.Position.xz, center.xz) <= reach * reach)
            {
                visitor.Visit(entry);
            }
        }
    }
}
