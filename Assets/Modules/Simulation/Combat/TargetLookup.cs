using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Read-only view of other entities for combat jobs: alive, hostile, where and how big.</summary>
    public struct TargetLookup
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<Faction> _factions;
        [ReadOnly] private ComponentLookup<NavAgent> _agents;
        [ReadOnly] private ComponentLookup<NavObstacle> _obstacles;

        public TargetLookup(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _health = state.GetComponentLookup<Health>(true);
            _factions = state.GetComponentLookup<Faction>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _health.Update(ref state);
            _factions.Update(ref state);
            _agents.Update(ref state);
            _obstacles.Update(ref state);
        }

        /// <summary>False once destroyed or at zero health (dying entities linger until the frame ends).</summary>
        public bool IsAlive(Entity entity) => _health.TryGetComponent(entity, out var health) && health.Current > 0f;

        public bool IsValidTarget(Entity target, byte attackerFaction, in FactionRelations relations) =>
            IsAlive(target) && _factions.TryGetComponent(target, out var faction) &&
            relations.IsHostile(attackerFaction, faction.Value);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        public float Radius(Entity entity) => Footprint.Radius(entity, _agents, _obstacles);
    }
}
