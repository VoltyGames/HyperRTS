using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Transport;
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
        [ReadOnly] private ComponentLookup<Inside> _inside;

        public TargetLookup(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _health = state.GetComponentLookup<Health>(true);
            _factions = state.GetComponentLookup<Faction>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _inside = state.GetComponentLookup<Inside>(true);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _health.Update(ref state);
            _factions.Update(ref state);
            _agents.Update(ref state);
            _obstacles.Update(ref state);
            _inside.Update(ref state);
        }

        /// <summary>False once destroyed or at zero health (dying entities linger until the frame ends).</summary>
        public bool IsAlive(Entity entity) => _health.TryGetComponent(entity, out var health) && health.Current > 0f;

        /// <summary>Alive, hostile and not tucked inside a container.</summary>
        public bool IsValidTarget(Entity target, byte attackerFaction, in FactionRelations relations) =>
            IsAlive(target) && !IsInside(target) && _factions.TryGetComponent(target, out var faction) &&
            relations.IsHostile(attackerFaction, faction.Value);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        /// <summary>Passengers take their container's footprint, so they fire from its edge.</summary>
        public float Radius(Entity entity) =>
            EntityRadius.Of(IsInside(entity) ? _inside[entity].Container : entity, _agents, _obstacles);

        private bool IsInside(Entity entity) => _inside.HasComponent(entity) && _inside.IsComponentEnabled(entity);
    }
}
