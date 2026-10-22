using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>Snapshot of one living, ownable entity, taken at the start of the movement phase.</summary>
    public struct SpatialEntry
    {
        public Entity Entity;
        public float3 Position;
        public float Radius;
        public byte Faction;

        /// <summary>Has a <see cref="NavAgent"/>, so it moves and crowds other units.</summary>
        public bool IsUnit;

        /// <summary>Its <see cref="MoveDestination"/> is enabled.</summary>
        public bool IsMoving;

        /// <summary>Its <see cref="Arrival"/> is enabled: its last move ended by itself.</summary>
        public bool IsArrived;

        /// <summary>The <see cref="MoveDestination.Group"/> of its current or last move.</summary>
        public int Group;

        /// <summary>Unit forward on XZ, the lane a mover drives along.</summary>
        public float2 Facing;

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

        /// <summary>Bounding radius of the footprint plus its spacing, for <see cref="SpatialIndex.QueryHulls{T}"/>.</summary>
        public float HullReach;

        /// <summary>
        /// Fixes <see cref="HullReach"/> once, so queries skip the length, and files the entry by its centre cell or,
        /// wider than a cell, in the oversized list.
        /// </summary>
        public void AddTo(NativeParallelMultiHashMap<int, SpatialEntry>.ParallelWriter cells,
            NativeList<SpatialEntry>.ParallelWriter oversized, float cellSize)
        {
            HullReach = Radius + math.length(SegmentAxis) + Spacing;
            if (HullReach > cellSize)
            {
                oversized.AddNoResize(this);
                return;
            }

            cells.Add(SpatialIndex.Key(SpatialIndex.CellAt(Position, cellSize)), this);
        }

        /// <summary>From this entry's core segment to another's, between their closest points.</summary>
        public readonly float2 ClosestOffset(float2 position, float2 axis)
        {
            // Closest points of segments P(s) and Q(t), s and t in -1..1 (Ericson, Real-Time Collision Detection 5.1.9).
            var r = position - Position.xz;
            var a = math.lengthsq(axis);
            var e = math.lengthsq(SegmentAxis);
            var b = math.dot(axis, SegmentAxis);
            var c = math.dot(axis, r);
            var f = math.dot(SegmentAxis, r);
            var denominator = a * e - b * b;
            var s = denominator > 1e-6f ? math.clamp((b * f - c * e) / denominator, -1f, 1f) : 0f;
            var t = e > 1e-6f ? (b * s + f) / e : 0f;
            if (t < -1f || t > 1f)
            {
                t = math.clamp(t, -1f, 1f);
                s = a > 1e-6f ? math.clamp((b * t - c) / a, -1f, 1f) : 0f;
            }
            else if (a > 1e-6f && e <= 1e-6f)
            {
                s = math.clamp(-c / a, -1f, 1f);
            }

            return r + axis * s - SegmentAxis * t;
        }
    }
}
