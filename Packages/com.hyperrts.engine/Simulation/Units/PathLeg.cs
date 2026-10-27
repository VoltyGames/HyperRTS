using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>
    /// The stretch of path a unit is on: its next corner, the turn there and how far is left, and on a planned
    /// manoeuvre its gear and where it changes.
    /// </summary>
    public readonly struct PathLeg
    {
        // Past this much beyond the look-ahead, a gear change is too far off to brake for yet.
        private const float GearScan = 10f;

        public readonly float2 Corner;
        public readonly float2 Direction;
        public readonly float Distance;

        /// <summary>Radians the path turns at <see cref="Corner"/>; 0 at the goal.</summary>
        public readonly float CornerTurn;

        /// <summary>Path length from the unit to the goal.</summary>
        public readonly float Remaining;

        public readonly bool IsFinal;

        /// <summary>Heading of the stretch after <see cref="Corner"/>; this leg's own at the goal.</summary>
        public readonly float2 Next;

        /// <summary>On a planned manoeuvre (<see cref="PathWaypoint.Tight"/>), which it follows rather than cuts.</summary>
        public readonly bool Tight;

        /// <summary>Driven backwards.</summary>
        public readonly bool Reverse;

        /// <summary>Path length to the next gear change, or a way past the look-ahead without one, on a tight leg.</summary>
        public readonly float ToGearChange;

        /// <summary>The point a tight leg's pursuit aims at, no further than the next gear change.</summary>
        public readonly float2 Aim;

        public PathLeg(float2 position, float2 corner, float2 next, float cornerTurn, float remaining, bool isFinal)
        {
            var toCorner = corner - position;
            Corner = corner;
            Distance = math.length(toCorner);
            Direction = math.normalizesafe(toCorner);
            CornerTurn = cornerTurn;
            Remaining = remaining;
            IsFinal = isFinal;
            Next = isFinal ? Direction : next;
            Tight = false;
            Reverse = false;
            ToGearChange = remaining;
            Aim = corner;
        }

        private PathLeg(in PathLeg leg, bool reverse, float toGearChange, float2 aim)
        {
            this = leg;
            Tight = true;
            Reverse = reverse;
            ToGearChange = toGearChange;
            Aim = aim;
        }

        /// <summary>Where the leg stops: the goal, or on a tight leg the next gear change.</summary>
        public float Stop => Tight ? math.min(ToGearChange, Remaining) : Remaining;

        /// <summary>The same leg bent to head along <paramref name="velocity"/>, as avoidance steers it.</summary>
        public PathLeg Toward(float2 position, float2 velocity) =>
            new(position, position + math.normalizesafe(velocity) * Distance, Next, CornerTurn, Remaining, IsFinal);

        /// <summary>
        /// The point <paramref name="distance"/> along the path from <paramref name="position"/>, round the corner;
        /// the goal itself on the final leg.
        /// </summary>
        public float2 PointAhead(float2 position, float distance)
        {
            if (IsFinal)
            {
                return Corner;
            }

            return Distance >= distance ? position + Direction * distance : Corner + Next * (distance - Distance);
        }

        public bool HasArrived(float radius) => IsFinal && Distance <= NavTolerances.Arrive(radius);

        /// <summary>
        /// The leg to the first waypoint, or straight to the goal while the search is pending. The path's length past
        /// that waypoint comes cached in <see cref="PathState.LengthPastCorner"/>.
        /// </summary>
        public static PathLeg Along(float2 position, float2 goal, DynamicBuffer<PathWaypoint> waypoints,
            in PathState path)
        {
            if (waypoints.Length == 0)
            {
                return new PathLeg(position, goal, float2.zero, 0f, math.distance(position, goal), true);
            }

            var corner = waypoints[0].Position.xz;
            var remaining = math.distance(position, corner) + path.LengthPastCorner;
            var isFinal = waypoints.Length == 1;
            var next = isFinal ? float2.zero : math.normalizesafe(waypoints[1].Position.xz - corner);
            var turn = isFinal ? 0f : Turn(corner - position, next);
            return new PathLeg(position, corner, next, turn, remaining, isFinal);
        }

        /// <summary>
        /// This leg on a planned manoeuvre, with its gear, the distance to the next gear change and the point
        /// <paramref name="lookAhead"/> along the path short of it; unchanged off one. In reverse the rear axle leads,
        /// so the look-ahead grows by its offset.
        /// </summary>
        public PathLeg Manoeuvre(float2 position, DynamicBuffer<PathWaypoint> waypoints, float lookAhead,
            in Chassis chassis)
        {
            if (waypoints.Length == 0 || !waypoints[0].Tight)
            {
                return this;
            }

            var reverse = waypoints[0].Reverse;
            lookAhead += reverse ? chassis.RearAxle : 0f;
            var travelled = 0f;
            var from = position;
            var aim = Corner;
            var aimed = false;
            for (var i = 0; i < waypoints.Length && travelled <= lookAhead + GearScan; i++)
            {
                var to = waypoints[i].Position.xz;
                var length = math.distance(from, to);
                if (!aimed)
                {
                    aimed = travelled + length >= lookAhead;
                    aim = aimed ? from + (to - from) * ((lookAhead - travelled) / length) : to;
                }

                travelled += length;
                if (i + 1 < waypoints.Length && waypoints[i + 1].Reverse != reverse)
                {
                    return new PathLeg(this, reverse, travelled, aim);
                }

                from = to;
            }

            return new PathLeg(this, reverse, travelled, aim);
        }

        private static float Turn(float2 into, float2 outOf)
        {
            var cos = math.dot(math.normalizesafe(into), math.normalizesafe(outOf));
            return math.acos(math.clamp(cos, -1f, 1f));
        }
    }
}
