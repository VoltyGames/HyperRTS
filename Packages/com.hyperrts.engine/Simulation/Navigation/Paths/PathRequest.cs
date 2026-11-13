using HyperRTS.Simulation.Navigation.Agents;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Paths
{
    /// <summary>One pending path search, snapshotted so the search job needs no component reads.</summary>
    internal struct PathRequest
    {
        // A route this many hull lengths or shorter that starts behind the hull is backed, as a driver would.
        public const float ShortWayHulls = 3f;
        public const float BehindCos = -0.3f;

        public Entity Entity;
        public float3 Start;

        /// <summary>The unit's facing on XZ, where the route starts it.</summary>
        public float2 Facing;

        public float3 Goal;

        public NavAgent Agent;

        /// <summary><see cref="MoveDestination.Backward"/>: the order asks to drive there in reverse.</summary>
        public bool Backward;

        /// <summary><see cref="MoveDestination.Group"/>: members of one share a flow field.</summary>
        public int Group;

        public uint Frame;

        /// <summary>Why the unit asked, for <see cref="PathStats.SearchesByCause"/>.</summary>
        public PathCause Cause;

        public PathRequest(Entity entity, in LocalTransform transform, in MoveDestination destination,
            in NavAgent agent, in PathState path)
        {
            Entity = entity;
            Start = transform.Position;
            Facing = math.normalizesafe(transform.Forward().xz);
            Goal = destination.Value;
            Group = destination.Group;
            Backward = destination.Backward;
            Agent = agent;
            Frame = path.RequestFrame;
            Cause = path.Cause;
        }

        /// <summary>
        /// Whether a vehicle that reverses drives the whole route backwards: the order asks it to, or the route is
        /// short and starts behind it.
        /// </summary>
        public readonly bool DrivesBackward(NativeList<float3> points)
        {
            if (!Agent.CanReverse || points.Length == 0)
            {
                return false;
            }

            return Backward || IsShortWayBehind(points);
        }

        private readonly bool IsShortWayBehind(NativeList<float3> points)
        {
            var length = 0f;
            var from = Start.xz;
            var first = float2.zero;
            foreach (var point in points)
            {
                var step = point.xz - from;
                if (math.all(first == 0f) && math.lengthsq(step) > 1f)
                {
                    first = math.normalize(step);
                }

                length += math.length(step);
                from = point.xz;
            }

            return length <= ShortWayHulls * Agent.HullLength && math.dot(Facing, first) < BehindCos;
        }
    }
}
