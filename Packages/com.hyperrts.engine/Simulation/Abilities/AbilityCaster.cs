using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Status;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Fires an ability: starts its cooldown, publishes the event and runs the built-in effects. One thread only.</summary>
    public struct AbilityCaster
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        private PrefabSpawner _spawner;
        private EventWriter<AbilityActivation> _events;
        private EventWriter<DamageEvent> _damage;
        private EventWriter<StatusRequest> _status;

        public AbilityCaster(ref SystemState state)
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _spawner = new PrefabSpawner(ref state);
            _events = new EventWriter<AbilityActivation>(ref state);
            _damage = new EventWriter<DamageEvent>(ref state);
            _status = new EventWriter<StatusRequest>(ref state);
        }

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _spawner.Update(ref state);
            _events.Update(ref state);
            _damage.Update(ref state);
            _status.Update(ref state);
        }

        public void Cast(ref Ability ability, Entity caster, byte faction, Entity target, float3 position,
            EntityCommandBuffer ecb)
        {
            ability.CooldownRemaining = ability.Cooldown;
            _events.Add(new AbilityActivation
            {
                Caster = caster, Faction = faction, AbilityId = ability.Id, Target = target, Position = position,
            });

            if (ability.SpawnPrefab != Entity.Null)
            {
                _spawner.Spawn(ecb, ability.SpawnPrefab, position, faction);
            }

            if (ability.StatusEffect != Entity.Null && ability.Target == AbilityTarget.Entity)
            {
                _status.Add(new StatusRequest
                {
                    Target = target, Definition = ability.StatusEffect, Source = caster, SourceFaction = faction,
                });
            }

            if (ability.Damage != 0f)
            {
                _damage.Add(new DamageEvent
                {
                    Target = ability.Target == AbilityTarget.Entity ? target : Entity.Null,
                    Position = position,
                    Origin = _transforms.TryGetComponent(caster, out var from) ? from.Position : position,
                    Source = caster,
                    SourceFaction = faction,
                    Amount = ability.Damage,
                    Type = ability.DamageType,
                    GarrisonFactor = ability.GarrisonFactor,
                    Radius = ability.Radius,
                    EdgeFactor = 1f,
                    FriendlyFire = ability.FriendlyFire,
                });
            }
        }
    }
}
