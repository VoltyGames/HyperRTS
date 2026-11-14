using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation.Reach
{
    /// <summary>
    /// Walks units up to a target entity's footprint for orders that act on it (build, repair, capture, board,
    /// gather). Read-only, so jobs can share it.
    /// </summary>
    public struct TargetReach
    {
        /// <summary>How far a unit's edge may be from the nearest it can get to a footprint.</summary>
        public const float Slack = 1f;

        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<NavObstacle> _obstacles;
        [ReadOnly] private ComponentLookup<NavAgent> _agents;
        private float _navCellSize;

        public TargetReach(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _navCellSize = 0f;
        }

        public void Update(ref SystemState state, in MapSettings map)
        {
            _transforms.Update(ref state);
            _obstacles.Update(ref state);
            _agents.Update(ref state);
            _navCellSize = map.NavCellSize;
        }

        /// <summary>
        /// Gap from a unit's centre to a footprint within which it is at it, at any heading: its hull is then at most
        /// the slack and a nav cell off (closed cells reach up to a cell past the footprint).
        /// </summary>
        public static float Reach(float unitHalfLength, float navCellSize) => unitHalfLength + Slack + navCellSize;

        /// <summary>Whether the unit at <paramref name="unit"/> is at the footprint.</summary>
        public static bool InReach(in NavBox footprint, float2 unit, float unitHalfLength, float navCellSize) =>
            footprint.Gap(unit) <= Reach(unitHalfLength, navCellSize);

        /// <summary>Whether a unit standing at <paramref name="point"/> is at the target, as a spot it takes there is.</summary>
        public bool IsBy(Entity target, float2 point, in NavAgent agent) =>
            Footprint(target).Gap(point) <= Reach(agent.HalfLength, _navCellSize);

        /// <summary>Whether the target is an aircraft off the surface below it, over the heads of units on the ground.</summary>
        public bool IsAloft(Entity target, in NavGrid grid) =>
            _agents.TryGetComponent(target, out var agent) && agent.IsAloft(_transforms[target].Position, grid);

        /// <summary>The entity is still in the world (destroyed targets lose their transform first).</summary>
        public bool Exists(Entity entity) => _transforms.HasComponent(entity);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        public LocalTransform Transform(Entity entity) => _transforms[entity];

        /// <summary>
        /// Walks the unit to a spot of its own beside the target until in reach, then stops it; with every spot taken
        /// it waits clear of them. A walk that locomotion ended by itself short of a goal by the target is stuck.
        /// </summary>
        public ApproachState Approach(Entity target, in LocalTransform unit, in NavAgent agent, MoveControl move,
            in NavGrid grid, ReachClaims claims)
        {
            var footprint = Footprint(target);
            var position = unit.Position;
            if (InReach(footprint, position.xz, agent.HalfLength, _navCellSize))
            {
                move.Stop();
                return ApproachState.Reached;
            }

            // A goal already by the target stays put, so the path isn't restarted as the nearest edge slides.
            var goal = move.Destination.ValueRO.Value.xz;
            var isGoalByTarget = footprint.Gap(goal) <= Reach(agent.HalfLength, _navCellSize);
            if (isGoalByTarget && move.Arrived.ValueRO && !move.Moving.ValueRO)
            {
                return ApproachState.Stuck;
            }

            if (isGoalByTarget && move.Moving.ValueRO && Keeps(target, unit, agent, goal, claims))
            {
                return ApproachState.Walking;
            }

            var search = new StandSpotSearch(grid, footprint, unit, agent);
            if (search.TryFind(target, claims, out var spot))
            {
                claims.Take(target, spot);
                move.Destination.ValueRW.MoveTo(move.Moving, new float3(spot.x, position.y, spot.y));
                move.Arrived.ValueRW = false;
                return ApproachState.Walking;
            }

            Wait(search, footprint, unit, agent, move);
            return ApproachState.Walking;
        }

        // A goal another unit came to hold is chosen again, as is one a swerve or a push left inside the turning
        // circle, where no arc reaches it.
        private static bool Keeps(Entity target, in LocalTransform unit, in NavAgent agent, float2 goal,
            in ReachClaims claims)
        {
            if (claims.IsTaken(target, goal, StandSpotSearch.Spacing(agent)))
            {
                return false;
            }

            var facing = math.normalizesafe(unit.Forward().xz);
            return !agent.Chassis.IsInsideTurn(unit.Position.xz, facing, goal, 1f);
        }

        // Every spot is held, so the unit waits off the footprint, where one leaving a spot has room to back out:
        // it stands once that near and drives there from farther off.
        private static void Wait(in StandSpotSearch search, in NavBox footprint, in LocalTransform unit,
            in NavAgent agent, MoveControl move)
        {
            var gap = StandSpotSearch.WaitGap(agent);
            if (footprint.Gap(unit.Position.xz) <= gap + agent.HullLength)
            {
                move.Stop();
                return;
            }

            var goal = move.Destination.ValueRO.Value.xz;
            var isGoalWaitPoint = math.abs(footprint.Gap(goal) - gap) <= Slack;
            if (isGoalWaitPoint && move.Moving.ValueRO)
            {
                return;
            }

            var point = search.WaitPoint;
            move.Destination.ValueRW.MoveTo(move.Moving, new float3(point.x, unit.Position.y, point.y));
            move.Arrived.ValueRW = false;
        }

        /// <summary>
        /// The box a target is reached by: its footprint, the box round a unit's hull such as a transport's, or
        /// <see cref="EntityRadius"/> round a footprintless node.
        /// </summary>
        public NavBox Footprint(Entity target)
        {
            var transform = _transforms[target];
            if (_obstacles.TryGetComponent(target, out var obstacle))
            {
                return new NavBox(transform.Position, transform.Rotation, obstacle.Size);
            }

            if (_agents.TryGetComponent(target, out var agent))
            {
                var hull = new float2(agent.Radius, agent.HalfLength) * 2f;
                return new NavBox(transform.Position, transform.Rotation, hull);
            }

            return new NavBox(transform.Position, transform.Rotation, new float2(EntityRadius.Default * 2f));
        }
    }
}
