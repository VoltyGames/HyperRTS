using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a unit's move gets on: progress is the path left shrinking or the hull turning. Without it the unit
    /// re-plans the way to its next corner every <see cref="RepairSeconds"/> and gives up after
    /// <see cref="GiveUpSeconds"/>.
    /// </summary>
    public struct MoveProgress : IComponentData
    {
        public const float RepairSeconds = 0.75f;
        public const float GiveUpSeconds = 3f;

        // About 20 degrees of turn is progress, so a pivot or a three-point turn never stalls.
        private const float TurnedCos = 0.94f;

        /// <summary>Shortest path left so far, measured on the current path.</summary>
        public float Best;

        /// <summary>Facing at the last progress.</summary>
        public float2 Facing;

        /// <summary>Seconds since the last progress.</summary>
        public float Stalled;

        /// <summary>Value of <see cref="Stalled"/> at which the next repair is due.</summary>
        public float NextRepair;

        /// <summary>Path status the measure was taken on; a new path starts a new baseline.</summary>
        public PathStatus Seen;

        public bool IsTracking;

        /// <summary>Measures the path left and asks for a repair when due; true once the unit gives up.</summary>
        public bool HasGivenUp(in LocalTransform transform, in NavAgent agent, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints, ref PathState path, float deltaTime)
        {
            var position = transform.Position.xz;
            var remaining = waypoints.Length > 0
                ? math.distance(position, waypoints[0].Position.xz) + path.LengthPastCorner
                : math.distance(position, goal.xz);
            var facing = math.normalizesafe(math.mul(transform.Rotation, math.forward()).xz);
            var margin = NavTolerances.Arrive(agent.Radius);
            if (Track(remaining, facing, path.Status, margin, deltaTime) && waypoints.Length > 0)
            {
                path.Status = PathStatus.None;
                path.Repair = true;
            }

            return Stalled >= GiveUpSeconds;
        }

        /// <summary>Records this frame's path left; true when the way to the next corner is due a repair.</summary>
        private bool Track(float remaining, float2 facing, PathStatus status, float margin, float deltaTime)
        {
            if (!IsTracking)
            {
                this = new MoveProgress { NextRepair = RepairSeconds, IsTracking = true };
                Rebase(remaining, facing, status);
                return false;
            }

            // A new path is measured afresh, but the clock runs on so a unit that keeps re-planning still gives up.
            if (status != Seen)
            {
                Rebase(remaining, facing, status);
                return false;
            }

            if (remaining < Best - margin || math.dot(facing, Facing) < TurnedCos)
            {
                Rebase(remaining, facing, status);
                Stalled = 0f;
                NextRepair = RepairSeconds;
                return false;
            }

            Stalled += deltaTime;
            if (Stalled < NextRepair)
            {
                return false;
            }

            NextRepair += RepairSeconds;
            return true;
        }

        private void Rebase(float remaining, float2 facing, PathStatus status)
        {
            Best = remaining;
            Facing = facing;
            Seen = status;
        }
    }
}
