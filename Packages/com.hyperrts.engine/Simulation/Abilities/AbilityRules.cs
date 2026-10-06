using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
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

        /// <summary>Entity-targeted abilities need a living target matching the filter; the others accept anything.</summary>
        public static bool IsValidTarget(in Ability ability, Entity target, byte faction, in TargetLookup targets,
            in ComponentLookup<Faction> factions, in FactionRelations relations)
        {
            if (ability.Target != AbilityTarget.Entity)
            {
                return true;
            }

            if (!targets.IsAlive(target) || !factions.TryGetComponent(target, out var owner))
            {
                return false;
            }

            return ability.Filter switch
            {
                AbilityTargetFilter.Hostile => relations.IsHostile(faction, owner.Value),
                AbilityTargetFilter.Allied => relations.IsAllied(faction, owner.Value),
                _ => true,
            };
        }

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
