using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Power;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>How close an enemy must be before a self-targeted ability (smoke, self-heal) is worth firing.</summary>
        private const float SelfCastRange = 10f;

        /// <summary>Where and at whom an ability will be fired.</summary>
        private struct Aim
        {
            public Entity Target;
            public float3 Position;
        }

        /// <summary>Fires one ready ability per caster, then every ready player power, at a target that suits it.</summary>
        private void UseAbilities(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var casters = snapshot.Casters;
            for (var i = 0; i < casters.Length; i++)
            {
                var caster = casters.Entities[i];
                if (!casters.IsOwnedBy(i, turn.Faction) || !CanCast(ref state, caster))
                {
                    continue;
                }

                foreach (var ability in SystemAPI.GetBuffer<Ability>(caster))
                {
                    if (TryAim(ref state, ability, caster, casters.Position(i), true, turn.Faction, snapshot, out var aim))
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

            foreach (var power in SystemAPI.GetBuffer<Ability>(turn.Player))
            {
                if (TryAim(ref state, power, turn.Player, turn.Home, false, turn.Faction, snapshot, out var aim))
                {
                    turn.Commands.Add(Command(CommandType.UsePower, Entity.Null, power, aim));
                }
            }
        }

        private static PlayerCommand Command(CommandType type, Entity caster, in Ability ability, in Aim aim) => new()
        {
            Type = type, Unit = caster, Target = aim.Target, Position = aim.Position, Argument = ability.Id,
        };

        private static bool CanCast(ref SystemState state, Entity caster)
        {
            if (ConstructionRules.IsUnderConstruction(state.EntityManager, caster))
            {
                return false;
            }

            return !PowerRules.IsUnpowered(state.EntityManager, caster);
        }

        /// <summary>
        /// Self-targeted abilities fire when an enemy is near; aimed ones at the nearest suitable target, which must be
        /// in range when <paramref name="ranged"/> (player powers reach anywhere).
        /// </summary>
        private bool TryAim(ref SystemState state, in Ability ability, Entity caster, float3 from, bool ranged, byte faction,
            in Snapshot snapshot, out Aim aim)
        {
            aim = new Aim { Target = Entity.Null, Position = from };
            if (!ability.IsReady)
            {
                return false;
            }

            if (ability.RequiredTypeId != 0 && !snapshot.Completed.Owns(faction, ability.RequiredTypeId))
            {
                return false;
            }

            if (ability.Target == AbilityTarget.None)
            {
                return HostileWithin(faction, from, math.max(ability.Radius, SelfCastRange), snapshot);
            }

            var index = BestTarget(ref state, ability, caster, from, ranged, faction, snapshot);
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
        private int BestTarget(ref SystemState state, in Ability ability, Entity caster, float3 from, bool ranged, byte faction,
            in Snapshot snapshot)
        {
            var targets = snapshot.Targets;
            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                var position = targets.Position(i);
                var distance = math.distancesq(position.xz, from.xz);
                var bestEntity = best < 0 ? Entity.Null : targets.Entities[best];
                if (!IsCloser(distance, entity, bestDistance, bestEntity))
                {
                    continue;
                }

                if (ranged && !AbilityRules.InRange(ability, caster, from, entity, position, _targetLookup))
                {
                    continue;
                }

                if (Suits(ref state, ability, entity, faction, snapshot))
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>Heals (allied filter or negative damage) go to hurt allies; everything else at enemies.</summary>
        private bool Suits(ref SystemState state, in Ability ability, Entity entity, byte faction, in Snapshot snapshot)
        {
            if (!AbilityRules.IsValidTarget(ability, entity, faction, _targetLookup, _factions, snapshot.Relations))
            {
                return false;
            }

            var supportive = ability.Filter == AbilityTargetFilter.Allied || ability.Damage < 0f;
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
                var near = math.distancesq(targets.Position(i).xz, from.xz) <= range * range;
                if (near && _targetLookup.IsValidTarget(targets.Entities[i], faction, snapshot.Relations))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
