using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Production;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>
    /// The fixed definition of a cooldown ability on a unit, building or player entity, with optional built-in effects.
    /// Every use is published as an <see cref="AbilityActivation"/>; the running cooldown is the replicated <see cref="AbilityCooldown"/> with the same id.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct Ability : IBufferElementData
    {
        /// <summary>Hash of the name; commands and events refer to it.</summary>
        public int Id;

        public FixedString32Bytes Name;
        public UnityObjectRef<Texture2D> Icon;
        public AbilityTarget Target;
        public AbilityTargetFilter Filter;

        /// <summary>Edge-to-edge cast range; 0 is unlimited.</summary>
        public float Range;

        public float Cooldown;

        /// <summary>Building type the owner must have completed; 0 for none.</summary>
        public int RequiredTypeId;

        public Entity SpawnPrefab;
        /// <summary>Dealt round the target when its amount is set.</summary>
        public DamageProfile Damage;

        /// <summary>Status effect prefab applied to an entity target; <c>Entity.Null</c> for none.</summary>
        public Entity StatusEffect;

        /// <summary>Index of the ability with this id, or -1.</summary>
        public static int Find(DynamicBuffer<Ability> abilities, int id)
        {
            for (var i = 0; i < abilities.Length; i++)
            {
                if (abilities[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Ready and its required building owned; with <see cref="IsValidTarget"/> checked on issue and firing.</summary>
        public readonly bool IsAvailable(in AbilityCooldown state, byte owner, in CompletedBuildings completed)
        {
            if (!state.IsReady)
            {
                return false;
            }

            return RequiredTypeId == 0 || completed.Owns(owner, RequiredTypeId);
        }

        /// <summary>
        /// Entity-targeted abilities need a living target outside any container that matches the filter; the others
        /// accept anything.
        /// </summary>
        public readonly bool IsValidTarget(Entity target, byte owner, in TargetLookup targets,
            in ComponentLookup<Owner> owners, in OwnerRelations relations)
        {
            if (Target != AbilityTarget.Entity)
            {
                return true;
            }

            if (!owners.TryGetComponent(target, out var targetOwner))
            {
                return false;
            }

            var hostile = relations.IsHostile(owner, targetOwner.Value);
            if (!MatchesFilter(hostile, relations.IsAllied(owner, targetOwner.Value)))
            {
                return false;
            }

            // Enemies follow weapon targeting, so an ability can't find what stealth hides from guns.
            if (hostile)
            {
                return targets.IsValidTarget(target, owner, relations);
            }

            return targets.IsAlive(target) && !targets.IsInside(target);
        }

        /// <summary>Where the ability lands: the caster itself, the clicked point, or the target entity.</summary>
        public readonly float3 Aim(float3 caster, Entity target, float3 point, in TargetLookup targets) =>
            Target switch
            {
                AbilityTarget.None => caster,
                AbilityTarget.Entity => targets.Position(target),
                _ => point,
            };

        public readonly bool InRange(Entity caster, float3 from, Entity target, float3 aim, in TargetLookup targets)
        {
            if (Range <= 0f || Target == AbilityTarget.None)
            {
                return true;
            }

            var targetRadius = Target == AbilityTarget.Entity ? targets.Radius(target) : 0f;
            return EntityRadius.EdgeDistance(from, targets.Radius(caster), aim, targetRadius) <= Range;
        }

        private readonly bool MatchesFilter(bool hostile, bool allied) =>
            Filter switch
            {
                AbilityTargetFilter.Hostile => hostile,
                AbilityTargetFilter.Allied => allied,
                _ => true,
            };
    }
}
