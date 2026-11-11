using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
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

        /// <summary>A caster that isn't <see cref="Offline"/>; the AI and player commands both cast only from these.</summary>
        public static bool CanCast(EntityManager entityManager, Entity caster) => !entityManager.HasEnabled<Offline>(caster);

        public void Update(ref SystemState state)
        {
            _transforms.Update(ref state);
            _spawner.Update(ref state);
            _events.Update(ref state);
            _damage.Update(ref state);
            _status.Update(ref state);
        }

        /// <summary>Fires the ability; <paramref name="activation"/> says who, at what and where, and is published as is.</summary>
        public void Cast(in Ability ability, ref AbilityCooldown state, in AbilityActivation activation,
            EntityCommandBuffer ecb)
        {
            var caster = activation.Caster;
            var owner = activation.Owner;
            var target = activation.Target;
            var position = activation.Position;
            state.Remaining = ability.Cooldown;
            _events.Add(activation);

            if (ability.SpawnPrefab != Entity.Null)
            {
                _spawner.Spawn(ecb, ability.SpawnPrefab, position, owner);
            }

            if (ability.StatusEffect != Entity.Null && ability.Target == AbilityTarget.Entity)
            {
                _status.Add(new StatusRequest
                {
                    Target = target, Definition = ability.StatusEffect, Source = caster, SourceOwner = owner,
                });
            }

            if (ability.Damage.Amount != 0f)
            {
                var struck = ability.Target == AbilityTarget.Entity ? target : Entity.Null;
                var origin = _transforms.TryGetComponent(caster, out var from) ? from.Position : position;
                _damage.Add(ability.Damage.Hit(caster, owner, origin, struck, position, WeaponTargets.Surface));
            }
        }
    }
}
