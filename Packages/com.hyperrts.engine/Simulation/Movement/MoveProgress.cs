using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Navigation.Paths;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Movement
{
    /// <summary>
    /// Whether a unit's move gets anywhere, by the path left shrinking or the hull turning, so a stalled unit replans
    /// and in the end gives up.
    /// </summary>
    public struct MoveProgress : IComponentData
    {
        public const float ReplanSeconds = 1f;
        public const float MaxReplanSeconds = 4f;
        public const float GiveUpSeconds = 3f;

        // Stuck within this many radii of the path's end, the goal is taken; farther out it is traffic that clears.
        public const float NearEndRadii = 4f;

        // Turning earns bounded time for a pivot or a three-point turn, never a fresh distance record.
        private const float TurnedCos = 0.94f;

        /// <summary>Shortest path left so far, measured on the current path.</summary>
        public float Best;

        /// <summary>Facing at the last progress.</summary>
        public float2 Facing;

        /// <summary>Seconds since the last progress.</summary>
        public float Stalled;

        /// <summary>Time credited to turning since the last improvement in remaining distance.</summary>
        public float Turning;

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

        /// <summary>One frame's reading of a unit: the path left to its goal, its facing and its radius.</summary>
        public readonly struct Reading
        {
            public readonly float Remaining;
            public readonly float2 Facing;
            public readonly float Radius;

            public Reading(float2 position, float2 facing, float radius, float3 goal,
                DynamicBuffer<PathWaypoint> waypoints, in PathState path)
            {
                Remaining = waypoints.Length > 0
                    ? path.Remaining(position, waypoints)
                    : math.distance(position, goal.xz);
                Facing = facing;
                Radius = radius;
            }
        }

        /// <summary>
        /// Measures the path left and asks for a replan when due, backing off from <see cref="ReplanSeconds"/> to
        /// <see cref="MaxReplanSeconds"/>; true once stalled <see cref="GiveUpSeconds"/> near the end or held by walls.
        /// </summary>
        public bool HasGivenUp(in Reading reading, ref PathState path, WallHold hold, float deltaTime)
        {
            if (path.Status != PathStatus.Ready)
            {
                return false;
            }

            var remaining = reading.Remaining;
            var radius = reading.Radius;
            var facing = reading.Facing;
            var margin = NavTolerances.Arrive(radius);
            if (Track(remaining, facing, margin, hold == WallHold.Sunk, deltaTime))
            {
                path.Replan();
                Replanned = true;
            }

            // Held by walls alone, no replan frees it, however far the goal.
            var nearEnd = remaining <= radius * NearEndRadii + margin;
            return Stalled >= GiveUpSeconds && (hold != WallHold.None || nearEnd);
        }

        /// <summary>
        /// Before a vehicle held by walls gives up, its driver drives the other way once
        /// (<see cref="Locomotion.TurnOut"/>) with the clock restarted; true when it does.
        /// </summary>
        public bool TryTurnOut(ref Driver driver, WallHold hold)
        {
            if (hold == WallHold.None || TurnedOut || !driver.Handling.CanTurnOut)
            {
                return false;
            }

            TurnedOut = true;
            Stalled = 0f;
            NextReplan = ReplanSeconds;
            driver.Handling.TurnOut(ref driver.State);
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
            if (closer)
            {
                TurnedOut = false;
                Restart(remaining, facing);
                return false;
            }

            Stalled += deltaTime;
            if (turned && Turning < MaxReplanSeconds)
            {
                CreditTurn(facing);
                return false;
            }

            return IsReplanDue();
        }

        private bool IsReplanDue()
        {
            if (Stalled < NextReplan)
            {
                return false;
            }

            NextReplan += math.min(NextReplan, MaxReplanSeconds);
            return true;
        }

        private void CreditTurn(float2 facing)
        {
            var credit = math.min(Stalled, MaxReplanSeconds - Turning);
            Turning += credit;
            Stalled -= credit;
            Facing = facing;
        }

        private void Restart(float remaining, float2 facing)
        {
            Best = remaining;
            Facing = facing;
            Stalled = 0f;
            Turning = 0f;
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
