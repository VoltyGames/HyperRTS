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

        // Cell rings searched round a stand point for open ground the unit can reach.
        private const int StandRings = 2;

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
        /// Gap from a unit's core segment to a footprint within which it is at it. Closed cells reach up to a cell past
        /// the footprint and the unit keeps its radius off them, so its edge is allowed a cell more than the slack.
        /// </summary>
        public static float Reach(float unitRadius, float navCellSize) => unitRadius + Slack + navCellSize;

        /// <summary>Whether the unit's capsule round <paramref name="unit"/> is at the footprint.</summary>
        public static bool InReach(in NavBox footprint, float2 unit, float2 axis, float unitRadius,
            float navCellSize) =>
            footprint.Gap(unit - axis, unit + axis) <= Reach(unitRadius, navCellSize);

        /// <summary>The entity is still in the world (destroyed targets lose their transform first).</summary>
        public bool Exists(Entity entity) => _transforms.HasComponent(entity);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        public LocalTransform Transform(Entity entity) => _transforms[entity];

        /// <summary>
        /// Walks the unit to open ground beside the target until in reach, then stops it. A walk that locomotion ended
        /// by itself short of a goal still by the target is stuck.
        /// </summary>
        public ApproachState Approach(Entity target, in LocalTransform unit, in NavAgent agent, MoveControl move,
            in NavGrid grid)
        {
            var footprint = Footprint(target);
            var position = unit.Position;
            if (InReach(footprint, position.xz, agent.SegmentAxis(unit.Rotation), agent.Radius, _navCellSize))
            {
                move.Stop();
                return ApproachState.Reached;
            }

            // A goal already by the target stays put, so the path isn't restarted as the nearest edge slides.
            var goal = move.Destination.ValueRO.Value.xz;
            var isGoalByTarget = footprint.Gap(goal, goal) <= Reach(agent.Radius, _navCellSize);
            if (isGoalByTarget && move.Arrived.ValueRO && !move.Moving.ValueRO)
            {
                return ApproachState.Stuck;
            }

            if (isGoalByTarget && move.Moving.ValueRO)
            {
                return ApproachState.Walking;
            }

            var edge = StandPoint(grid, footprint, position.xz, agent);
            move.Destination.ValueRW.MoveTo(move.Moving, new float3(edge.x, position.y, edge.y));
            move.Arrived.ValueRW = false;
            return ApproachState.Walking;
        }

        /// <summary>
        /// Where the unit stands at the footprint: reachable open ground by the nearest edge, else by another side
        /// when a neighbour sits flush against that edge.
        /// </summary>
        private readonly float2 StandPoint(in NavGrid grid, in NavBox footprint, float2 unit, in NavAgent agent)
        {
            var offset = agent.Radius + Slack * 0.5f;
            var edge = footprint.EdgePoint(unit, offset);
            if (!grid.IsCreated || grid.Classes.Find(agent.Class) < 0)
            {
                return edge;
            }

            var access = agent.Access(grid);
            var region = access.NearestRegion(new float3(unit.x, 0f, unit.y));
            if (TryStandNear(access, footprint, edge, region, agent.Radius, out var stand))
            {
                return stand;
            }

            var best = edge;
            var bestDistance = float.MaxValue;
            for (var side = 0; side < 4; side++)
            {
                var point = SidePoint(footprint, unit, side, offset);
                if (TryStandNear(access, footprint, point, region, agent.Radius, out stand)
                    && math.distancesq(unit, stand) < bestDistance)
                {
                    best = stand;
                    bestDistance = math.distancesq(unit, stand);
                }
            }

            return best;
        }

        /// <summary>
        /// The point itself when it is open ground in the unit's region, else the nearest such cell, if in reach.
        /// </summary>
        private readonly bool TryStandNear(in NavAccess access, in NavBox footprint, float2 point, int region,
            float radius, out float2 stand)
        {
            stand = point;
            var cell = access.Grid.Grid.Cell(new float3(point.x, 0f, point.y));
            if (region == 0 || !access.TryFindNearest(cell, StandRings, region, out var open))
            {
                return false;
            }

            if (!open.Equals(cell))
            {
                stand = access.Grid.Grid.Center(open);
            }

            return footprint.Gap(stand) <= Reach(radius, _navCellSize);
        }

        /// <summary>The point of the box's <paramref name="side"/> nearest the unit, pushed out by offset.</summary>
        private static float2 SidePoint(in NavBox box, float2 unit, int side, float offset)
        {
            var axis = side < 2 ? box.Right : box.Forward;
            var across = side < 2 ? box.Forward : box.Right;
            var half = side < 2 ? box.Half : box.Half.yx;
            var sign = side % 2 == 0 ? 1f : -1f;
            var along = math.clamp(math.dot(unit - box.Center, across), -half.y, half.y);
            return box.Center + axis * sign * (half.x + offset) + across * along;
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
