using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Spatial
{
    /// <summary>
    /// Singleton uniform-grid hash of living units and buildings, rebuilt each frame by
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

        public readonly int2 CellOf(float3 position) => CellOf(position, CellSize);

        public static int2 CellOf(float3 position, float cellSize) => (int2)math.floor(position.xz / cellSize);

        /// <summary>Unique for cells within ±32k of the origin, so queries never see foreign cells twice.</summary>
        public static int Key(int2 cell) => (cell.x & 0xFFFF) | (cell.y << 16);

        public static int2 CellOfKey(int key) => new((short)(key & 0xFFFF), key >> 16);

        /// <summary>Visits entries whose circle overlaps the query circle on the XZ plane.</summary>
        public readonly void Query<T>(float3 center, float radius, ref T visitor) where T : struct, ISpatialVisitor
        {
            var search = radius + CellSize;
            var min = CellOf(center - search);
            var max = CellOf(center + search);

            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    if (!Cells.TryGetFirstValue(Key(new int2(x, y)), out var entry, out var iterator))
                    {
                        continue;
                    }

                    do
                    {
                        VisitIfOverlapping(entry, center, radius, ref visitor);
                    } while (Cells.TryGetNextValue(out entry, ref iterator));
                }
            }

            foreach (var entry in Oversized)
            {
                VisitIfOverlapping(entry, center, radius, ref visitor);
            }
        }

        private static void VisitIfOverlapping<T>(in SpatialEntry entry, float3 center, float radius, ref T visitor)
            where T : struct, ISpatialVisitor
        {
            var reach = radius + entry.Radius;
            if (math.distancesq(entry.Position.xz, center.xz) <= reach * reach)
            {
                visitor.Visit(entry);
            }
        }
    }
}
