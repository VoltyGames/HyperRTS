using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Read-only view of other entities for combat jobs: alive, hostile, detected, hittable, where and how big.
    /// </summary>
    public struct TargetLookup
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        [ReadOnly] private ComponentLookup<Health> _health;
        [ReadOnly] private ComponentLookup<Faction> _factions;
        [ReadOnly] private ComponentLookup<NavAgent> _agents;
        [ReadOnly] private ComponentLookup<NavObstacle> _obstacles;
        [ReadOnly] private ComponentLookup<Inside> _inside;
        [ReadOnly] private ComponentLookup<Stealthed> _stealthed;
        [ReadOnly] private FogOfWar _fog;

        public TargetLookup(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _health = state.GetComponentLookup<Health>(true);
            _factions = state.GetComponentLookup<Faction>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _obstacles = state.GetComponentLookup<NavObstacle>(true);
            _inside = state.GetComponentLookup<Inside>(true);
            _stealthed = state.GetComponentLookup<Stealthed>(true);
            FogQuery(ref state).TryGetSingleton(out _fog);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _health.Update(ref state);
            _factions.Update(ref state);
            _agents.Update(ref state);
            _obstacles.Update(ref state);
            _inside.Update(ref state);
            _stealthed.Update(ref state);
            FogQuery(ref state).TryGetSingleton(out _fog);
        }

        /// <summary>False once destroyed or at zero health (dying entities linger until the frame ends).</summary>
        public bool IsAlive(Entity entity) => Health.IsAlive(_health, entity);

        /// <summary>Alive, hostile, not tucked inside a container and not hidden from the attacker by stealth.</summary>
        public bool IsValidTarget(Entity target, byte attackerFaction, in FactionRelations relations)
        {
            if (!IsAlive(target) || IsInside(target) || !_factions.TryGetComponent(target, out var faction))
            {
                return false;
            }

            var hostile = relations.IsHostile(attackerFaction, faction.Value);
            return hostile && !IsCloakedFrom(target, relations.TeamOf(attackerFaction));
        }

        /// <summary>A valid target (see above) that a weapon reaching <paramref name="targets"/> can hit.</summary>
        public bool IsValidTarget(Entity target, byte attackerFaction, in FactionRelations relations,
            WeaponTargets targets) =>
            IsValidTarget(target, attackerFaction, relations) && CanHit(target, targets);

        public bool CanHit(Entity target, WeaponTargets targets)
        {
            return CombatMath.CanHit(targets, NavAgent.LayerOf(_agents, target));
        }

        /// <summary>
        /// Stealthed and outside every detector of the team (see <see cref="FogOfWar.IsCloakedFrom"/>). Checks stealth
        /// first so unstealthed candidates skip the transform lookup.
        /// </summary>
        public bool IsCloakedFrom(Entity target, byte team) =>
            _stealthed.HasEnabled(target) && !_fog.IsDetected(Position(target), team);

        public float3 Position(Entity entity) => _transforms[entity].Position;

        /// <summary>Passengers take their container's footprint, so they fire from its edge.</summary>
        public float Radius(Entity entity) =>
            EntityRadius.Of(IsInside(entity) ? _inside[entity].Container : entity, _agents, _obstacles);

        /// <summary>Tucked inside a transport or garrison: hidden and untargetable.</summary>
        public bool IsInside(Entity entity) => _inside.HasEnabled(entity);

        /// <summary>
        /// The system caches the query, so this is a lookup after the first call, which (from the constructor) makes
        /// the system's jobs wait for the fog restamp. A stored query couldn't travel into jobs, and Burst only lets
        /// the [ReadOnly] fog field be filled through <c>out</c>.
        /// </summary>
        private static EntityQuery FogQuery(ref SystemState state) =>
            new EntityQueryBuilder(Allocator.Temp).WithAll<FogOfWar>().Build(ref state);
    }
}
