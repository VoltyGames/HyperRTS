using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a unit's move gets on: progress is the path left shrinking or the hull turning. One check: no progress for
    /// <see cref="ReplanSeconds"/> plans the whole path again, backing off to <see cref="MaxReplanSeconds"/>, and
    /// after <see cref="GiveUpSeconds"/> near its path's end the unit gives up. Farther out a jam is traffic that
    /// clears. A path still pending isn't measured.
    /// </summary>
    public struct MoveProgress : IComponentData
    {
        public const float ReplanSeconds = 1f;
        public const float MaxReplanSeconds = 4f;
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

        /// <summary>Value of <see cref="Stalled"/> at which the next replan is due.</summary>
        public float NextReplan;

        /// <summary>A replan was asked for, so the next path is measured afresh while the clock runs on.</summary>
        public bool Replanned;

        public bool IsTracking;

        /// <summary>Measures the path left and asks for a replan when due; true once the unit gives up.</summary>
        public bool HasGivenUp(float2 position, float2 facing, in NavAgent agent, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints, ref PathState path, float deltaTime)
        {
            if (path.Status != PathStatus.Ready)
            {
                return false;
            }

            var remaining = waypoints.Length > 0
                ? math.distance(position, waypoints[0].Position.xz) + path.LengthPastCorner
                : math.distance(position, goal.xz);
            var margin = NavTolerances.Arrive(agent.Radius);
            if (Track(remaining, facing, margin, deltaTime))
            {
                path.Status = PathStatus.None;
                Replanned = true;
            }

            return Stalled >= GiveUpSeconds && remaining <= agent.Radius * NearEndRadii + margin;
        }

        /// <summary>Records this frame's path left; true when a replan is due.</summary>
        private bool Track(float remaining, float2 facing, float margin, float deltaTime)
        {
            if (!IsTracking)
            {
                IsTracking = true;
                Restart(remaining, facing);
                return false;
            }

            // A replanned path is measured afresh, but the clock runs on so endless replans still give up.
            if (Replanned)
            {
                Replanned = false;
                Best = remaining;
                Facing = facing;
                return false;
            }

            if (remaining < Best - margin || math.dot(facing, Facing) < TurnedCos)
            {
                Restart(remaining, facing);
                return false;
            }

            Stalled += deltaTime;
            if (Stalled < NextReplan)
            {
                return false;
            }

            NextReplan += math.min(NextReplan, MaxReplanSeconds);
            return true;
        }

        private void Restart(float remaining, float2 facing)
        {
            Best = remaining;
            Facing = facing;
            Stalled = 0f;
            NextReplan = ReplanSeconds;
        }

        /// <summary>A move that just ended arrived or stalled; the next one measures progress afresh.</summary>
        public void Report(bool wasMoving, bool isMoving, bool stalled, ref Arrival arrival,
            EnabledRefRW<Arrival> arrived)
        {
            if (isMoving)
            {
                arrived.ValueRW = false;
                return;
            }

            IsTracking = false;
            Replanned = false;
            if (wasMoving)
            {
                arrived.ValueRW = true;
                arrival.Stalled = stalled;
            }
        }
    }
}
