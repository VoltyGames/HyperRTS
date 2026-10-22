using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Walks units up to a target entity's footprint for orders that act on it (build, repair, capture, board,
    /// gather). Read-only, so jobs can share it. How near counts as at it follows the map's nav cell size.
    /// </summary>
    public struct TargetReach
    {
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

        /// <summary>The entity is still in the world (destroyed targets lose their transform first).</summary>
        public bool Exists(Entity entity) => _transforms.HasComponent(entity);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        public LocalTransform Transform(Entity entity) => _transforms[entity];

        /// <summary>Walks the unit toward the target's edge until in reach, then stops it; true once there.</summary>
        public bool Approach(Entity target, in LocalTransform unit, in NavAgent agent, ref MoveDestination destination,
            EnabledRefRW<MoveDestination> moving)
        {
            var axis = agent.SegmentAxis(unit.Rotation);
            return ReachMath.Approach(ref destination, moving, Footprint(target), unit.Position, axis, agent.Radius,
                _navCellSize);
        }

        // A unit such as a transport is reached by the box round its hull, a footprintless node by EntityRadius.
        private NavBox Footprint(Entity target)
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
