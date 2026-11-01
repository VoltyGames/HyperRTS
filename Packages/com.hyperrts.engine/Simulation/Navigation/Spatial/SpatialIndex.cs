using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>
    /// Singleton uniform grid of living units and buildings over the map, rebuilt each frame by
    /// <see cref="SpatialIndexSystem"/>: entries sorted by cell, so a query scans each row of cells in one run and
    /// meets neighbours in the same order every time. Read it with <c>SystemAPI.GetSingleton</c> so job dependencies
    /// chain.
    /// </summary>
    public struct SpatialIndex : IComponentData
    {
        /// <summary>The map in <see cref="SpatialIndexSystem.CellSize"/> cells; entries off it file under the nearest edge cell.</summary>
        public GridFrame Frame;

        /// <summary>Where each cell's run in <see cref="Entries"/> starts, and one past the last cell, where they end.</summary>
        public NativeList<int> CellStarts;

        public NativeList<SpatialEntry> Entries;

        /// <summary>
        /// Entries wider than a cell (airfields, shipyards). Cells hold the rest by centre, so queries pad by one cell
        /// and scan this short list instead of padding every query by the widest building.
        /// </summary>
        public NativeList<SpatialEntry> Oversized;

        public readonly float CellSize => Frame.CellSize;

        /// <summary>The cell an entry at the position files under, clamped onto the map.</summary>
        public readonly int CellIndex(float3 position) => Frame.Index(Frame.Clamp(Frame.Cell(position)));

        /// <summary>How many entries the cell holds.</summary>
        public readonly int CountIn(int cell) => CellStarts[cell + 1] - CellStarts[cell];

        /// <summary>Visits entries whose circle overlaps the query circle on the XZ plane.</summary>
        public readonly void Query<T>(float3 center, float radius, ref T visitor) where T : struct, ISpatialVisitor =>
            Query(center, radius, false, ref visitor);

        /// <summary>Visits entries whose <see cref="SpatialEntry.HullReach"/> overlaps the query circle.</summary>
        public readonly void QueryHulls<T>(float3 center, float radius, ref T visitor)
            where T : struct, ISpatialVisitor =>
            Query(center, radius, true, ref visitor);

        private readonly void Query<T>(float3 center, float radius, bool hulls, ref T visitor)
            where T : struct, ISpatialVisitor
        {
            var search = radius + CellSize;
            var min = Frame.Clamp(Frame.Cell(center.xz - search));
            var max = Frame.Clamp(Frame.Cell(center.xz + search));
            // A row's cells sit side by side, so their entries form one run.
            for (var y = min.y; y <= max.y; y++)
            {
                var end = CellStarts[Frame.Index(new int2(max.x, y)) + 1];
                for (var i = CellStarts[Frame.Index(new int2(min.x, y))]; i < end; i++)
                {
                    VisitIfOverlapping(Entries[i], center, radius, hulls, ref visitor);
                }
            }

            foreach (var entry in Oversized)
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
