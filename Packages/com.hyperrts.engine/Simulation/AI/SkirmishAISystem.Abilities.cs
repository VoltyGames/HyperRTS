using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>Where and at whom an ability will be fired.</summary>
        private struct Aim
        {
            public Entity Target;
            public float3 Position;
        }

        /// <summary>Who fires an ability and from where; player powers are not ranged.</summary>
        private readonly struct Caster
        {
            public readonly Entity Entity;
            public readonly float3 From;
            public readonly bool Ranged;

            public Caster(Entity entity, float3 from, bool ranged)
            {
                Entity = entity;
                From = from;
                Ranged = ranged;
            }
        }

        /// <summary>Fires one ready ability per caster, then every ready player power, at a target that suits it.</summary>
        private void UseAbilities(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var casters = snapshot.Casters;
            for (var i = 0; i < casters.Length; i++)
            {
                var caster = casters.Entities[i];
                if (!casters.IsOwnedBy(i, turn.Faction) || !AbilityRules.CanCast(state.EntityManager, caster))
                {
                    continue;
                }

                var abilities = SystemAPI.GetBuffer<Ability>(caster);
                var cooldowns = SystemAPI.GetBuffer<AbilityCooldown>(caster);
                foreach (var ability in abilities)
                {
                    var ready = cooldowns[AbilityRules.FindCooldown(cooldowns, ability.Id)].IsReady;
                    if (ready && TryAim(ref state, ability, new Caster(caster, casters.Position(i), true), turn, snapshot,
                        out var aim))
                    {
                        turn.Commands.Add(Command(CommandType.UseAbility, caster, ability, aim));
                        break;
                    }
                }
            }

            if (!SystemAPI.HasBuffer<Ability>(turn.Player))
            {
                return;
            }

            var powers = SystemAPI.GetBuffer<Ability>(turn.Player);
            var charges = SystemAPI.GetBuffer<AbilityCooldown>(turn.Player);
            foreach (var power in powers)
            {
                var ready = charges[AbilityRules.FindCooldown(charges, power.Id)].IsReady;
                if (ready && TryAim(ref state, power, new Caster(turn.Player, turn.Home, false), turn, snapshot,
                        out var aim))
                {
                    turn.Commands.Add(Command(CommandType.UsePower, Entity.Null, power, aim));
                }
            }
        }

        private static PlayerCommand Command(CommandType type, Entity caster, in Ability ability, in Aim aim) => new()
        {
            Type = type, Unit = caster, Target = aim.Target, Position = aim.Position, Argument = ability.Id,
        };

        /// <summary>
        /// Self-targeted abilities fire when an enemy is near; aimed ones at the nearest suitable target, which must be
        /// in range when the caster is ranged (player powers reach anywhere).
        /// </summary>
        private bool TryAim(ref SystemState state, in Ability ability, in Caster caster, in Turn turn,
            in Snapshot snapshot, out Aim aim)
        {
            var faction = turn.Faction;
            var from = caster.From;
            aim = new Aim { Target = Entity.Null, Position = from };
            if (ability.RequiredTypeId != 0 && !snapshot.Completed.Owns(faction, ability.RequiredTypeId))
            {
                return false;
            }

            if (ability.Target == AbilityTarget.None)
            {
                var range = math.max(ability.Damage.Radius, SystemAPI.GetComponent<AIPlayer>(turn.Player).SelfAbilityTriggerRange);
                return HostileWithin(faction, from, range, snapshot);
            }

            var index = BestTarget(ref state, ability, caster, faction, snapshot);
            if (index < 0)
            {
                return false;
            }

            var target = snapshot.Targets.Entities[index];
            aim.Target = ability.Target == AbilityTarget.Entity ? target : Entity.Null;
            aim.Position = snapshot.Targets.Position(index);
            return true;
        }

        /// <summary>Index in the snapshot's targets of the nearest entity the ability suits, or -1.</summary>
        private int BestTarget(ref SystemState state, in Ability ability, in Caster caster, byte faction,
            in Snapshot snapshot)
        {
            var from = caster.From;
            var targets = snapshot.Targets;
            var closest = Closest.None;
            for (var i = 0; i < targets.Length; i++)
            {
                // Distance first, so the costlier range and suitability checks only run for would-be winners.
                var entity = targets.Entities[i];
                var distance = targets.DistanceSq(i, from);
                if (!closest.IsCloserThanBest(distance, entity))
                {
                    continue;
                }

                if (caster.Ranged && !AbilityRules.InRange(ability, caster.Entity, from, entity, targets.Position(i), _targetLookup))
                {
                    continue;
                }

                if (Suits(ref state, ability, entity, faction, snapshot))
                {
                    closest.Consider(i, entity, distance);
                }
            }

            return closest.Index;
        }

        /// <summary>Heals (allied filter or negative damage) go to hurt allies; everything else at enemies.</summary>
        private bool Suits(ref SystemState state, in Ability ability, Entity entity, byte faction, in Snapshot snapshot)
        {
            if (!AbilityRules.IsValidTarget(ability, entity, faction, _targetLookup, _factions, snapshot.Relations))
            {
                return false;
            }

            var supportive = ability.Filter == AbilityTargetFilter.Allied || ability.Damage.Amount < 0f;
            if (!supportive)
            {
                return _targetLookup.IsValidTarget(entity, faction, snapshot.Relations);
            }

            var health = SystemAPI.GetComponent<Health>(entity);
            var allied = snapshot.Relations.IsAllied(faction, _factions[entity].Value);
            return allied && health.Current < health.Max;
        }

        private bool HostileWithin(byte faction, float3 from, float range, in Snapshot snapshot)
        {
            var targets = snapshot.Targets;
            for (var i = 0; i < targets.Length; i++)
            {
                var near = targets.DistanceSq(i, from) <= range * range;
                if (near && _targetLookup.IsValidTarget(targets.Entities[i], faction, snapshot.Relations))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
