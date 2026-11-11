using HyperRTS.Simulation.Common.Cells;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>
    /// Singleton uniform grid of living units and buildings, rebuilt each frame by <see cref="SpatialIndexSystem"/>.
    /// Entries sort by cell so queries meet neighbours in the same order every time; read it with
    /// <c>SystemAPI.GetSingleton</c> so job dependencies chain.
    /// </summary>
    public struct SpatialIndex : IComponentData
    {
        /// <summary>The map in <see cref="SpatialIndexSystem.CellSize"/> cells; entries off it file under the nearest edge cell.</summary>
        public CellGrid Grid;

        /// <summary>Where each cell's run in <see cref="Entries"/> starts, and one past the last cell, where they end.</summary>
        public NativeList<int> CellStarts;

        public NativeList<SpatialEntry> Entries;

        /// <summary>
        /// Entries wider than a cell (airfields, shipyards). Cells hold the rest by centre, so queries pad by one cell
        /// and scan this short list instead of padding every query by the widest building.
        /// </summary>
        public NativeList<SpatialEntry> Oversized;

        /// <summary>One flag per cell, set when a moving vehicle is close enough that a foot unit there may need its lane.</summary>
        public NativeList<byte> VehicleCells;

        public readonly float CellSize => Grid.CellSize;

        /// <summary>
        /// Metres past a moving vehicle's hull, from its centre, within which a foot unit looks at it; the unit's own
        /// hull adds at most one cell.
        /// </summary>
        public const float LaneSight = 10f;

        /// <summary>Whether a foot unit at the position is near enough to a moving vehicle to watch its lane.</summary>
        public readonly bool IsNearVehicleLane(float3 position) => VehicleCells[CellIndex(position)] != 0;

        /// <summary>Flags every cell a foot unit could stand in and see the moving vehicle.</summary>
        public void MarkVehicleLane(in SpatialEntry vehicle)
        {
            var radius = LaneSight + vehicle.HullReach + CellSize;
            var min = Grid.Clamp(Grid.Cell(vehicle.Position.xz - radius));
            var max = Grid.Clamp(Grid.Cell(vehicle.Position.xz + radius));
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    VehicleCells[Grid.Index(new int2(x, y))] = 1;
                }
            }
        }

        /// <summary>The cell an entry at the position files under, clamped onto the map.</summary>
        public readonly int CellIndex(float3 position) => Grid.Index(Grid.Clamp(Grid.Cell(position)));

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
            var min = Grid.Clamp(Grid.Cell(center.xz - search));
            var max = Grid.Clamp(Grid.Cell(center.xz + search));
            // A row's cells sit side by side, so their entries form one run.
            for (var y = min.y; y <= max.y; y++)
            {
                var end = CellStarts[Grid.Index(new int2(max.x, y)) + 1];
                for (var i = CellStarts[Grid.Index(new int2(min.x, y))]; i < end; i++)
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
