using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Where a building stands and which way it faces: one value through the ghost, the placement command, server
    /// validation and the AI, so what the player sees is what the server checks.
    /// </summary>
    public readonly struct BuildingSpot
    {
        private const float QuarterTurn = math.PI * 0.5f;

        // Degrees off a quarter turn that still count as one, so a drag along an axis lands its edges on cells.
        private const float QuarterSnapDegrees = 0.5f;

        public readonly float3 Center;

        /// <summary>Radians about Y; the front (+Z) points along (sin, cos).</summary>
        public readonly float Yaw;

        /// <summary>The unturned size: X across the front, Y front to back.</summary>
        public readonly float2 Footprint;

        public readonly PlacementSurface Surface;

        public BuildingSpot(float3 center, float yaw, float2 footprint, PlacementSurface surface)
        {
            Center = center;
            Yaw = yaw;
            Footprint = footprint;
            Surface = surface;
        }

        public quaternion Rotation => quaternion.RotateY(Yaw);

        /// <summary>The turned footprint as it stamps onto the nav grid.</summary>
        public NavBox Box => new(Center, Rotation, Footprint);

        /// <summary>
        /// Near a quarter turn, the yaw becomes exact and the edges land on cell boundaries; at any other angle the
        /// centre lands on the half-cell lattice.
        /// </summary>
        public BuildingSpot Snap(in MapSettings map)
        {
            var quarters = Yaw / QuarterTurn;
            var nearest = math.round(quarters);
            if (math.abs(quarters - nearest) * 90f > QuarterSnapDegrees)
            {
                var step = map.NavCellSize * 0.5f;
                var lattice = math.round((Center.xz - map.Min) / step) * step + map.Min;
                return new BuildingSpot(new float3(lattice.x, Center.y, lattice.y), Yaw, Footprint, Surface);
            }

            var exact = new BuildingSpot(Center, nearest * QuarterTurn, Footprint, Surface);
            var cells = math.max(1f, math.round(exact.Box.Extents * 2f / map.NavCellSize));
            var offset = math.select(float2.zero, new float2(0.5f), math.fmod(cells, 2f) > 0.5f);
            var local = (Center.xz - map.Min) / map.NavCellSize - offset;
            var snapped = (math.round(local) + offset) * map.NavCellSize + map.Min;
            return new BuildingSpot(new float3(snapped.x, Center.y, snapped.y), exact.Yaw, Footprint, Surface);
        }

        /// <summary>Lifted onto the visible top under its centre.</summary>
        public BuildingSpot Grounded(in NavGrid grid) =>
            new(new float3(Center.x, grid.SurfaceHeight(Center), Center.z), Yaw, Footprint, Surface);

        /// <summary>The same spot with <paramref name="margin"/> added on every side, as the AI keeps lanes open.</summary>
        public BuildingSpot Grown(float margin) => new(Center, Yaw, Footprint + 2f * margin, Surface);

        /// <summary>
        /// Inside the map and over free cells of the surface only: plain land or plain water, never a deck or a
        /// blocked cell. Open scenery such as trees doesn't count: the site clears it.
        /// </summary>
        public bool IsValid(in MapSettings map, in NavGrid grid)
        {
            var box = Box;
            var extents = new float3(box.Extents.x, 0f, box.Extents.y);
            var inside = map.Contains(Center - extents) && map.Contains(Center + extents);
            if (!math.isfinite(Yaw) || !inside)
            {
                return false;
            }

            grid.GetBoxArea(box, out var min, out var max);
            var land = false;
            var water = false;
            for (var y = min.y; y <= max.y; y++)
            {
                for (var x = min.x; x <= max.x; x++)
                {
                    var cell = new int2(x, y);
                    if (!grid.InBox(box, cell))
                    {
                        continue;
                    }

                    var surface = grid.Surface(cell);
                    if (Forbids(surface))
                    {
                        return false;
                    }

                    land |= surface == NavSurface.Land;
                    water |= surface == NavSurface.Water;
                }
            }

            return Surface != PlacementSurface.Shoreline || (land && water);
        }

        /// <summary>Whether one cell under the spot forbids it on its own, whatever the other cells hold.</summary>
        public bool Forbids(NavSurface cell)
        {
            switch (Surface)
            {
                case PlacementSurface.Water:
                    return cell != NavSurface.Water;
                case PlacementSurface.Shoreline:
                    return cell != NavSurface.Land && cell != NavSurface.Water;
                default:
                    return cell != NavSurface.Land;
            }
        }

        /// <summary>Touching is allowed, so buildings can sit flush at any angle.</summary>
        public bool Overlaps(in NavBox other) => Box.Overlaps(other);

        /// <summary>A unit capsule, its core segment <paramref name="axis"/> either side of its centre, reaches inside.</summary>
        public bool Overlaps(float2 position, float2 axis, float radius) =>
            Box.Gap(position - axis, position + axis) < radius;

        /// <summary>
        /// A ground or naval unit hostile to <paramref name="faction"/> stands on the spot, seen or not. Friendly units
        /// drive off by themselves.
        /// </summary>
        public bool IsBlockedByEnemies(in SpatialIndex index, in FactionRelations relations, byte faction)
        {
            var finder = new EnemyFinder { Spot = this, Relations = relations, Faction = faction };
            index.QueryHulls(Center, math.length(Footprint) * 0.5f, ref finder);
            return finder.Found;
        }

        private struct EnemyFinder : ISpatialVisitor
        {
            public BuildingSpot Spot;
            public FactionRelations Relations;
            public byte Faction;
            public bool Found;

            public void Visit(in SpatialEntry entry)
            {
                if (Found || !entry.IsUnit || entry.Layer == NavLayer.Air)
                {
                    return;
                }

                if (Relations.IsHostile(Faction, entry.Faction))
                {
                    Found = Spot.Overlaps(entry.Position.xz, entry.SegmentAxis, entry.Radius);
                }
            }
        }
    }
}
