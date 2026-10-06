using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Fires an ability: starts its cooldown, publishes the event and runs the built-in effects. One thread only.</summary>
    public struct AbilityActivator
    {
        [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
        private BufferLookup<AbilityActivation> _events;
        private DamageWriter _damage;
        private Entity _eventQueue;

        public AbilityActivator(ref SystemState state) : this()
        {
            _transforms = state.GetComponentLookup<LocalTransform>(true);
            _events = state.GetBufferLookup<AbilityActivation>();
            _damage = new DamageWriter(ref state);
        }

        public void Update(ref SystemState state, Entity damageQueue, Entity eventQueue)
        {
            _transforms.Update(ref state);
            _events.Update(ref state);
            _damage.Update(ref state, damageQueue);
            _eventQueue = eventQueue;
        }

        public void Activate(ref Ability ability, Entity caster, byte faction, Entity target, float3 position,
            EntityCommandBuffer ecb)
        {
            ability.CooldownRemaining = ability.Cooldown;
            _events[_eventQueue].Add(new AbilityActivation
            {
                Caster = caster, Faction = faction, AbilityId = ability.Id, Target = target, Position = position,
            });

            if (ability.SpawnPrefab != Entity.Null)
            {
                Spawn(ability.SpawnPrefab, faction, position, ecb);
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
                    Radius = ability.Radius,
                    EdgeFactor = 1f,
                    FriendlyFire = ability.FriendlyFire,
                });
            }
        }

        private void Spawn(Entity prefab, byte faction, float3 position, EntityCommandBuffer ecb)
        {
            // Keep the prefab's rotation and scale; only place the instance.
            var transform = _transforms.TryGetComponent(prefab, out var prefabTransform)
                ? prefabTransform
                : LocalTransform.Identity;
            transform.Position = position;

            var instance = ecb.Instantiate(prefab);
            ecb.AddComponent(instance, transform);
            ecb.AddComponent(instance, new Faction { Value = faction });
        }
    }
}
