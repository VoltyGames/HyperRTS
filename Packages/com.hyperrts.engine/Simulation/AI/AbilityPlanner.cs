using HyperRTS.Simulation.Abilities;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>Fires one ready ability per caster, then every ready player power, at a target that suits it.</summary>
    internal struct AbilityPlanner
    {
        private EntityQuery _casterQuery;
        private AIGroup _casters;
        private TargetLookup _targetLookup;
        private TargetQueries _targetQueries;
        private ComponentLookup<Owner> _owners;

        public static AbilityPlanner Create(ref SystemState state) => new()
        {
            _casterQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<Ability, AbilityCooldown, Owner, LocalTransform>().WithNone<Dead>().Build(ref state),
            _targetLookup = new TargetLookup(ref state),
            _targetQueries = TargetQueries.Create(ref state),
            _owners = state.GetComponentLookup<Owner>(true),
        };

        public void Update(ref SystemState state)
        {
            _targetLookup.Update(ref state, _targetQueries);
            _owners.Update(ref state);
            _casters = AIGroup.From(_casterQuery);
        }

        public void Plan(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            UseCasterAbilities(ref state, turn, snapshot);
            UsePlayerPowers(ref state, turn, snapshot);
        }

        private void UseCasterAbilities(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            for (var i = 0; i < _casters.Length; i++)
            {
                var caster = _casters.Entities[i];
                if (!_casters.IsOwnedBy(i, turn.Owner) || !AbilityRules.CanCast(entities, caster))
                {
                    continue;
                }

                var abilities = entities.GetBuffer<Ability>(caster);
                var cooldowns = entities.GetBuffer<AbilityCooldown>(caster);
                foreach (var ability in abilities)
                {
                    var ready = cooldowns[AbilityRules.FindCooldown(cooldowns, ability.Id)].IsReady;
                    if (ready && TryAim(ref state, turn, snapshot, ability, new Caster(caster, _casters.Position(i), true),
                            out var aim))
                    {
                        turn.Commands.Add(Command(CommandType.UseAbility, caster, ability, aim));
                        break;
                    }
                }
            }
        }

        private void UsePlayerPowers(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            if (!entities.HasBuffer<Ability>(turn.Player))
            {
                return;
            }

            var powers = entities.GetBuffer<Ability>(turn.Player);
            var charges = entities.GetBuffer<AbilityCooldown>(turn.Player);
            foreach (var power in powers)
            {
                var ready = charges[AbilityRules.FindCooldown(charges, power.Id)].IsReady;
                if (ready && TryAim(ref state, turn, snapshot, power, new Caster(turn.Player, turn.Home, false),
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
        private bool TryAim(ref SystemState state, in AITurn turn, in AISnapshot snapshot, in Ability ability,
            in Caster caster, out Aim aim)
        {
            aim = new Aim { Target = Entity.Null, Position = caster.From };
            if (ability.RequiredTypeId != 0 && !snapshot.Completed.Owns(turn.Owner, ability.RequiredTypeId))
            {
                return false;
            }

            if (ability.Target == AbilityTarget.None)
            {
                var triggerRange = state.EntityManager.GetComponentData<AIPlayer>(turn.Player).SelfAbilityTriggerRange;
                return HostileWithin(turn.Owner, caster.From, math.max(ability.Damage.Radius, triggerRange), snapshot);
            }

            var index = BestTarget(ref state, snapshot, ability, caster, turn.Owner);
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
        private int BestTarget(ref SystemState state, in AISnapshot snapshot, in Ability ability, in Caster caster,
            byte owner)
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

                if (Suits(ref state, snapshot, ability, entity, owner))
                {
                    closest.Consider(i, entity, distance);
                }
            }

            return closest.Index;
        }

        /// <summary>Heals (allied filter or negative damage) go to hurt allies; everything else at enemies.</summary>
        private bool Suits(ref SystemState state, in AISnapshot snapshot, in Ability ability, Entity entity, byte owner)
        {
            if (!AbilityRules.IsValidTarget(ability, entity, owner, _targetLookup, _owners, snapshot.Relations))
            {
                return false;
            }

            var supportive = ability.Filter == AbilityTargetFilter.Allied || ability.Damage.Amount < 0f;
            if (!supportive)
            {
                return _targetLookup.IsValidTarget(entity, owner, snapshot.Relations);
            }

            var health = state.EntityManager.GetComponentData<Health>(entity);
            var allied = snapshot.Relations.IsAllied(owner, _owners[entity].Value);
            return allied && health.Current < health.Max;
        }

        private bool HostileWithin(byte owner, float3 from, float range, in AISnapshot snapshot)
        {
            var targets = snapshot.Targets;
            for (var i = 0; i < targets.Length; i++)
            {
                var near = targets.DistanceSq(i, from) <= range * range;
                if (near && _targetLookup.IsValidTarget(targets.Entities[i], owner, snapshot.Relations))
                {
                    return true;
                }
            }

            return false;
        }

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
    }
}
