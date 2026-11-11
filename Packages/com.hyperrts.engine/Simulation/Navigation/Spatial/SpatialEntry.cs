using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation.Spatial
{
    /// <summary>Snapshot of one living, ownable entity, taken at the start of the movement phase.</summary>
    public struct SpatialEntry
    {
        public Entity Entity;
        public float3 Position;
        public float Radius;
        public byte Owner;

        /// <summary>Has a <see cref="NavAgent"/>, so it moves and crowds other units.</summary>
        public bool IsUnit;

        /// <summary>Its <see cref="MoveDestination"/> is enabled.</summary>
        public bool IsMoving;

        /// <summary>
        /// Its <see cref="Arrival"/> is enabled: its last move ended by itself, not short against a groupmate.
        /// </summary>
        public bool IsArrived;

        /// <summary>The <see cref="MoveDestination.Group"/> of its current or last move.</summary>
        public int Group;

        /// <summary>Unit forward on XZ, the lane a mover drives along.</summary>
        public float2 Facing;

        /// <summary>Unit direction on XZ from it to its move goal while it moves, else zero.</summary>
        public float2 Heading;

        /// <summary>Last frame's <see cref="Velocity"/>; zero for entities without one.</summary>
        public float2 Velocity;

        /// <summary>Has a <see cref="Common.BuildingTag"/>; destructible scenery is neither this nor a unit.</summary>
        public bool IsBuilding;

        /// <summary>The <see cref="NavAgent"/> layer; Ground for entities without one.</summary>
        public NavLayer Layer;

        /// <summary>Half the <see cref="NavAgent"/> capsule's core segment on XZ; zero for round footprints.</summary>
        public float2 SegmentAxis;

        /// <summary>The <see cref="NavAgent.Spacing"/> it keeps; zero for entities without an agent.</summary>
        public float Spacing;

        /// <summary>The <see cref="NavAgent.Mass"/> in tonnes; zero for entities without an agent.</summary>
        public float Mass;

        /// <summary>A <see cref="NavAgent.IsVehicle"/>: the crowd's push never moves it and allied foot units make way.</summary>
        public bool IsVehicle;

        /// <summary>Bounding radius of the footprint plus its spacing, for <see cref="SpatialIndex.QueryHulls{T}"/>.</summary>
        public float HullReach;

        /// <summary>Wider than a cell, so it goes in <see cref="SpatialIndex.Oversized"/> rather than by its centre.</summary>
        public readonly bool IsOversized(float cellSize) => HullReach > cellSize;

        /// <summary>Fixes <see cref="HullReach"/> once, so queries skip the length.</summary>
        public void FixReach() => HullReach = Radius + math.length(SegmentAxis) + Spacing;

        /// <summary>From this entry's core segment to another's, between their closest points.</summary>
        public readonly float2 ClosestOffset(float2 position, float2 axis) =>
            Planar.ClosestOffset(position, axis, Position.xz, SegmentAxis);

        /// <summary>
        /// The unit direction on XZ that parts this entry from <paramref name="other"/>. Crossed segments have no
        /// closest-point line, so it falls back to the centres, then to a tie-break.
        /// </summary>
        public readonly float2 DirectionAway(float2 offset, float distance, in SpatialEntry other)
        {
            if (distance > 1e-4f)
            {
                return offset / distance;
            }

            var centres = Position.xz - other.Position.xz;
            var apart = math.length(centres);
            return apart > 1e-4f ? centres / apart : TieBreak(other.Entity);
        }

        // Stacked units need opposite, deterministic directions: hash the pair, flip by index order.
        private readonly float2 TieBreak(Entity other)
        {
            var pair = new int2(math.min(Entity.Index, other.Index), math.max(Entity.Index, other.Index));
            math.sincos(math.hash(pair) * (math.PI * 2f / uint.MaxValue), out var sin, out var cos);
            return new float2(cos, sin) * (Entity.Index < other.Index ? 1f : -1f);
        }
    }
}
