using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Power;
using HyperRTS.Simulation.Production;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Abilities
{
    /// <summary>Lookup, targeting and range rules shared by ability commands and casting.</summary>
    public static class AbilityRules
    {
        public static int IndexOf(DynamicBuffer<Ability> abilities, int id)
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

        /// <summary>A caster that is finished and powered; the AI and player commands both cast only from these.</summary>
        public static bool CanCast(EntityManager entityManager, Entity caster)
        {
            if (entityManager.HasEnabled<ConstructionProgress>(caster))
            {
                return false;
            }

            return !entityManager.HasEnabled<Unpowered>(caster);
        }

        /// <summary>Ready, its required building owned and aimed at a valid target; checked on issue and firing.</summary>
        public static bool IsUsable(in Ability ability, Entity target, byte faction, in CompletedBuildings completed,
            in TargetLookup targets, in ComponentLookup<Faction> factions, in FactionRelations relations)
        {
            if (!ability.IsReady)
            {
                return false;
            }

            if (ability.RequiredTypeId != 0 && !completed.Owns(faction, ability.RequiredTypeId))
            {
                return false;
            }

            return IsValidTarget(ability, target, faction, targets, factions, relations);
        }

        /// <summary>
        /// Entity-targeted abilities need a living target outside any container that matches the filter; the others
        /// accept anything.
        /// </summary>
        public static bool IsValidTarget(in Ability ability, Entity target, byte faction, in TargetLookup targets,
            in ComponentLookup<Faction> factions, in FactionRelations relations)
        {
            if (ability.Target != AbilityTarget.Entity)
            {
                return true;
            }

            if (!factions.TryGetComponent(target, out var owner))
            {
                return false;
            }

            var hostile = relations.IsHostile(faction, owner.Value);
            if (!MatchesFilter(ability.Filter, hostile, relations.IsAllied(faction, owner.Value)))
            {
                return false;
            }

            // Enemies follow weapon targeting, so an ability can't find what stealth hides from guns.
            if (hostile)
            {
                return targets.IsValidTarget(target, faction, relations);
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
            return CombatMath.EdgeDistance(from, targets.Radius(caster), aim, targetRadius) <= ability.Range;
        }
    }
}
