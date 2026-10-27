using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>One pending path search, snapshotted so the search job needs no component reads.</summary>
    internal struct PathRequest
    {
        public Entity Entity;
        public float3 Start;

        /// <summary>The unit's facing on XZ, where the drive planner starts it.</summary>
        public float2 Facing;

        public float3 Goal;

        /// <summary><see cref="MoveDestination.Facing"/>: zero, or the pose the path must end in.</summary>
        public float2 GoalFacing;

        public float RunIn;
        public float ExitRun;
        public NavAgent Agent;

        /// <summary><see cref="MoveDestination.Group"/>: members of one share a flow field.</summary>
        public int Group;

        public uint Frame;

        /// <summary>Why the unit asked, for <see cref="PathStats.SearchesByCause"/>.</summary>
        public PathCause Cause;

        /// <summary>Walls held the unit when it asked, so the drive planner plans its way out from the start.</summary>
        public bool Stuck;

        public PathRequest(Entity entity, in LocalTransform transform, in MoveDestination destination,
            in NavAgent agent, in PathState path)
        {
            Entity = entity;
            Start = transform.Position;
            Facing = math.normalizesafe(transform.Forward().xz);
            Goal = destination.Value;
            GoalFacing = destination.Facing;
            RunIn = destination.RunIn;
            ExitRun = destination.ExitRun;
            Group = destination.Group;
            Agent = agent;
            Frame = path.RequestFrame;
            Cause = path.Cause;
            Stuck = path.Stuck;
        }

        /// <summary>Where the route starts: past the straight exit run, if there is one.</summary>
        public readonly float3 RouteStart => Start + new float3(Facing.x, 0f, Facing.y) * ExitRun;
    }
}
