using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a unit's move gets on: progress is the path left shrinking or the hull turning. Without it the unit
    /// re-plans the way to its next corner, backing off from <see cref="RepairSeconds"/> to
    /// <see cref="MaxRepairSeconds"/>, and gives up after <see cref="GiveUpSeconds"/> only near its path's end.
    /// </summary>
    public struct MoveProgress : IComponentData
    {
        public const float RepairSeconds = 0.75f;
        public const float MaxRepairSeconds = 6f;
        public const float GiveUpSeconds = 3f;

        // Stuck within this many radii of the path's end, the goal is taken; farther out it is traffic that clears.
        public const float NearEndRadii = 4f;

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

        /// <summary>Wait before the repair after next; doubles with each repair up to the cap.</summary>
        public float Backoff;

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

            return Stalled >= GiveUpSeconds && remaining <= agent.Radius * NearEndRadii + margin;
        }

        /// <summary>Records this frame's path left; true when the way to the next corner is due a repair.</summary>
        private bool Track(float remaining, float2 facing, PathStatus status, float margin, float deltaTime)
        {
            if (!IsTracking)
            {
                this = new MoveProgress { IsTracking = true };
                Restart(remaining, facing, status);
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
                Restart(remaining, facing, status);
                return false;
            }

            Stalled += deltaTime;
            if (Stalled < NextRepair)
            {
                return false;
            }

            NextRepair += Backoff;
            Backoff = math.min(Backoff * 2f, MaxRepairSeconds);
            return true;
        }

        private void Restart(float remaining, float2 facing, PathStatus status)
        {
            Rebase(remaining, facing, status);
            Stalled = 0f;
            NextRepair = RepairSeconds;
            Backoff = RepairSeconds * 2f;
        }

        private void Rebase(float remaining, float2 facing, PathStatus status)
        {
            Best = remaining;
            Facing = facing;
            Seen = status;
        }
    }
}
