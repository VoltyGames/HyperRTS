using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Units
{
    /// <summary>The stretch of path a unit is on: its next corner, the turn there and how far is left.</summary>
    public readonly struct PathLeg
    {
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
        }

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

        private static float Turn(float2 into, float2 outOf)
        {
            var cos = math.dot(math.normalizesafe(into), math.normalizesafe(outOf));
            return math.acos(math.clamp(cos, -1f, 1f));
        }
    }
}
