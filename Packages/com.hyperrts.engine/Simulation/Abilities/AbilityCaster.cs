using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Status;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Abilities;

/// <summary>Fires an ability: starts its cooldown, publishes the event and runs the built-in effects. One thread only.</summary>
public struct AbilityCaster
{
    [ReadOnly] private ComponentLookup<LocalTransform> _transforms;
    private PrefabSpawner _spawner;
    private EventWriter<AbilityActivation> _events;
    private EventWriter<DamageEvent> _damage;
    private EventWriter<StatusRequest> _status;
    [ReadOnly] private BufferLookup<StrikeAbility> _strikes;
    private bool _strategicAssets;

    public AbilityCaster(ref SystemState state)
    {
        _transforms = state.GetComponentLookup<LocalTransform>(true);
        _spawner = new PrefabSpawner(ref state);
        _events = new EventWriter<AbilityActivation>(ref state);
        _damage = new EventWriter<DamageEvent>(ref state);
        _status = new EventWriter<StatusRequest>(ref state);
        _strikes = state.GetBufferLookup<StrikeAbility>(true);
        _strategicAssets = true;
        state.RequireForUpdate<MatchRules>();
    }

    /// <summary>A caster that isn't <see cref="Offline"/>; the AI and player commands both cast only from these.</summary>
    public static bool CanCast(EntityManager entityManager, Entity caster) => !entityManager.HasEnabled<Offline>(caster);

    /// <summary>Refreshes the lookups and takes the match's strategic assets switch for this frame.</summary>
    public void Update(ref SystemState state, in MatchRules rules)
    {
        _transforms.Update(ref state);
        _spawner.Update(ref state);
        _events.Update(ref state);
        _damage.Update(ref state);
        _status.Update(ref state);
        _strikes.Update(ref state);
        _strategicAssets = rules.StrategicAssets;
    }

    /// <summary>A <see cref="StrikeAbility"/> while the match switches strategic assets off, which no one may cast.</summary>
    public readonly bool IsSwitchedOff(Entity caster, int abilityId)
    {
        if (_strategicAssets || !_strikes.TryGetBuffer(caster, out var strikes))
        {
            return false;
        }

        return StrikeAbility.Find(strikes, abilityId) >= 0;
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
