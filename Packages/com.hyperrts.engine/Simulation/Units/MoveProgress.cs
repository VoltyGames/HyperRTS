using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// How a unit's move gets on: progress is the path left shrinking or the hull turning. One check: no progress for
    /// <see cref="ReplanSeconds"/> plans the whole path again, backing off to <see cref="MaxReplanSeconds"/>, and
    /// after <see cref="GiveUpSeconds"/> near its path's end, or held by walls anywhere, the unit gives up. Farther
    /// out a jam is traffic that clears. A path still pending isn't measured.
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

        /// <summary>A turn-out from the walls was tried since the unit last got closer, so the next hold gives up.</summary>
        public bool TurnedOut;

        /// <summary>How walls hold a unit this frame.</summary>
        public enum WallHold : byte
        {
            None,

            /// <summary>Against walls, unmoved, with no other unit near: only the walls stop it.</summary>
            Wedged,

            /// <summary>Sunk in walls and getting no shallower.</summary>
            Sunk,
        }

        /// <summary>Measures the path left and asks for a replan when due; true once the unit gives up.</summary>
        public bool HasGivenUp(float2 position, float2 facing, in NavAgent agent, float3 goal,
            DynamicBuffer<PathWaypoint> waypoints, ref PathState path, WallHold hold, float deltaTime)
        {
            if (path.Status != PathStatus.Ready)
            {
                return false;
            }

            var remaining = waypoints.Length > 0
                ? math.distance(position, waypoints[0].Position.xz) + path.LengthPastCorner
                : math.distance(position, goal.xz);
            var margin = NavTolerances.Arrive(agent.Radius);
            if (Track(remaining, facing, margin, hold == WallHold.Sunk, deltaTime))
            {
                path.Replan();
                Replanned = true;
            }

            // Held by walls alone, no replan frees it, however far the goal.
            var nearEnd = remaining <= agent.Radius * NearEndRadii + margin;
            return Stalled >= GiveUpSeconds && (hold != WallHold.None || nearEnd);
        }

        /// <summary>
        /// Before a vehicle held by walls gives up, it drives the other way once (<see cref="Locomotion.TurnOut"/>)
        /// with the clock restarted; true when it does.
        /// </summary>
        public bool TryTurnOut(in Locomotion locomotion, ref LocomotionState drive, WallHold hold)
        {
            if (hold == WallHold.None || TurnedOut || !locomotion.CanTurnOut)
            {
                return false;
            }

            TurnedOut = true;
            Stalled = 0f;
            NextReplan = ReplanSeconds;
            locomotion.TurnOut(ref drive);
            return true;
        }

        /// <summary>Records this frame's path left; true when a replan is due.</summary>
        private bool Track(float remaining, float2 facing, float margin, bool sunk, float deltaTime)
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

            // A hull stuck sunk in walls turning on the spot gets nowhere.
            var turned = !sunk && math.dot(facing, Facing) < TurnedCos;
            var closer = remaining < Best - margin;
            if (closer || turned)
            {
                // Only getting closer earns another turn-out, so a hull can't rock in a wedge for ever.
                TurnedOut &= !closer;
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

        /// <summary>A move that just ended records why; the next one measures progress afresh.</summary>
        public void Report(bool wasMoving, bool isMoving, ArrivalReason reason, ref Arrival arrival,
            EnabledRefRW<Arrival> arrived)
        {
            if (isMoving)
            {
                arrived.ValueRW = false;
                return;
            }

            IsTracking = false;
            Replanned = false;
            TurnedOut = false;
            if (wasMoving)
            {
                arrived.ValueRW = true;
                arrival.Reason = reason;
            }
        }
    }
}
