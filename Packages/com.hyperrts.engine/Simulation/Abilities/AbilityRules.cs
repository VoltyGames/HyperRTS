using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Lookup, targeting and range rules shared by ability commands and casting.</summary>
    public static class AbilityRules
    {
        public static int FindAbility(DynamicBuffer<Ability> abilities, int id)
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

        /// <summary>Index of the cooldown for the ability id, or -1 when the entity has none yet.</summary>
        public static int FindCooldown(DynamicBuffer<AbilityCooldown> states, int id)
        {
            for (var i = 0; i < states.Length; i++)
            {
                if (states[i].Id == id)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>A caster that isn't <see cref="Offline"/>; the AI and player commands both cast only from these.</summary>
        public static bool CanCast(EntityManager entityManager, Entity caster) => !entityManager.HasEnabled<Offline>(caster);

        /// <summary>Ready and its required building owned; with <see cref="IsValidTarget"/> checked on issue and firing.</summary>
        public static bool IsAvailable(in Ability ability, in AbilityCooldown state, byte owner, in CompletedBuildings completed)
        {
            if (!state.IsReady)
            {
                return false;
            }

            return ability.RequiredTypeId == 0 || completed.Owns(owner, ability.RequiredTypeId);
        }

        /// <summary>
        /// Entity-targeted abilities need a living target outside any container that matches the filter; the others
        /// accept anything.
        /// </summary>
        public static bool IsValidTarget(in Ability ability, Entity target, byte owner, in TargetLookup targets,
            in ComponentLookup<Owner> owners, in OwnerRelations relations)
        {
            if (ability.Target != AbilityTarget.Entity)
            {
                return true;
            }

            if (!owners.TryGetComponent(target, out var targetOwner))
            {
                return false;
            }

            var hostile = relations.IsHostile(owner, targetOwner.Value);
            if (!MatchesFilter(ability.Filter, hostile, relations.IsAllied(owner, targetOwner.Value)))
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

        private static bool MatchesFilter(AbilityTargetFilter filter, bool hostile, bool allied) =>
            filter switch
            {
                AbilityTargetFilter.Hostile => hostile,
                AbilityTargetFilter.Allied => allied,
                _ => true,
            };

        /// <summary>Where the ability lands: the caster itself, the clicked point, or the target entity.</summary>
        public static float3 Aim(in Ability ability, float3 caster, Entity target, float3 point, in TargetLookup targets) =>
            ability.Target switch
            {
                AbilityTarget.None => caster,
                AbilityTarget.Entity => targets.Position(target),
                _ => point,
            };

        public static bool InRange(in Ability ability, Entity caster, float3 from, Entity target, float3 aim,
            in TargetLookup targets)
        {
            if (ability.Range <= 0f || ability.Target == AbilityTarget.None)
            {
                return true;
            }

            var targetRadius = ability.Target == AbilityTarget.Entity ? targets.Radius(target) : 0f;
            return EntityRadius.EdgeDistance(from, targets.Radius(caster), aim, targetRadius) <= ability.Range;
        }
    }
}
