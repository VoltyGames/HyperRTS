using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>
    /// Walks units up to a target entity's footprint for orders that act on it (build, repair, capture, board,
    /// gather). Read-only, so jobs can share it.
    /// </summary>
    public struct TargetReach
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<NavObstacle> _obstacles;

        public TargetReach(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _obstacles.Update(ref state);
        }

        /// <summary>The entity is still in the world (destroyed targets lose their transform first).</summary>
        public bool Exists(Entity entity) => _transforms.HasComponent(entity);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        /// <summary>Walks the unit toward <paramref name="target"/> until in reach, then stops it; true once there.</summary>
        public bool Approach(Entity target, float3 unit, float unitRadius, ref MoveDestination destination,
            EnabledRefRW<MoveDestination> moving)
        {
            var extents = ReachMath.HalfExtents(_obstacles, target);
            return ReachMath.Approach(ref destination, moving, unit, unitRadius, Position(target), extents);
        }
    }
}
