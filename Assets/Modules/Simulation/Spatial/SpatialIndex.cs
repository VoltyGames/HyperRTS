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

        public readonly int2 CellOf(float3 position) => (int2)math.floor(position.xz / CellSize);

        /// <summary>Unique for cells within ±32k of the origin, so queries never see foreign cells twice.</summary>
        public static int Key(int2 cell) => (cell.x & 0xFFFF) | (cell.y << 16);

        /// <summary>Visits entries whose circle overlaps the query circle on the XZ plane.</summary>
        public readonly void Query<T>(float3 center, float radius, ref T visitor) where T : struct, ISpatialVisitor
        {
            // Entries are filed by centre; the extra cell catches large ones (radius < CellSize) poking in.
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
                        var reach = radius + entry.Radius;
                        if (math.distancesq(entry.Position.xz, center.xz) <= reach * reach)
                        {
                            visitor.Visit(entry);
                        }
                    } while (Cells.TryGetNextValue(out entry, ref iterator));
                }
            }
        }
    }
}
