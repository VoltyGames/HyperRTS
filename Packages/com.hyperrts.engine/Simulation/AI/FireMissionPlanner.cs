using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Orders each idle unit whose weapon fires only on order to fire a mission at the densest group of enemies within
    /// its range, since such a weapon never picks targets itself. Like <see cref="AttackPlanner"/>, an AI limited by
    /// fog fires only at what its team has seen.
    /// </summary>
    internal struct FireMissionPlanner
    {
        private EntityQuery _query;
        private AIGroup _units;
        private TargetLookup _targetLookup;
        private TargetQueries _targetQueries;

        /// <summary>Default (not created) in a match without fog, where everything counts as known.</summary>
        private FogOfWar _fog;

        public static FireMissionPlanner Create(ref SystemState state) => new()
        {
            _query = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<WeaponMount, Owner, LocalTransform>().WithPresent<PointAim>().WithDisabled<ActiveOrder>()
                .WithNone<Dead>().Build(ref state),
            _targetLookup = new TargetLookup(ref state),
            _targetQueries = TargetQueries.Create(ref state),
        };

        public void Update(ref SystemState state)
        {
            _targetLookup.Update(ref state, _targetQueries);
            _units = AIGroup.From(_query);
            _fog = _targetQueries.Fog.TryGetSingleton(out FogOfWar fog) ? fog : default;
        }

        public readonly void Plan(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var entities = state.EntityManager;
            for (var i = 0; i < _units.Length; i++)
            {
                if (!turn.IsFree(_units, i))
                {
                    continue;
                }

                var unit = _units.Entities[i];
                var primary = entities.GetBuffer<WeaponMount>(unit, true)[0];
                if (primary.Ready && primary.Weapon.FiresAtGround
                    && TryChoose(entities, turn, snapshot, _units.Position(i), primary.Weapon, out var point))
                {
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.FireMission, Unit = unit, Position = point });
                    turn.Busy.Add(unit);
                }
            }
        }

        /// <summary>The enemy in range with the most enemies within a blast of it, nearest on a tie; false when none is in range.</summary>
        private readonly bool TryChoose(EntityManager entities, in AITurn turn, in AISnapshot snapshot, float3 from,
            in Weapon weapon, out float3 point)
        {
            var targets = snapshot.Targets;
            var blast = weapon.Damage.Radius + weapon.Spread;
            var bestCount = 0;
            var bestDistance = float.MaxValue;
            point = default;
            for (var i = 0; i < targets.Length; i++)
            {
                var distance = targets.DistanceSq(i, from);
                if (!weapon.Reaches(math.sqrt(distance)) || !IsKnownEnemy(entities, turn, snapshot, i))
                {
                    continue;
                }

                var count = CountWithin(entities, turn, snapshot, targets.Position(i), blast);
                if (count > bestCount || (count == bestCount && distance < bestDistance))
                {
                    bestCount = count;
                    bestDistance = distance;
                    point = targets.Position(i);
                }
            }

            return bestCount > 0;
        }

        private readonly int CountWithin(EntityManager entities, in AITurn turn, in AISnapshot snapshot, float3 centre,
            float radius)
        {
            var targets = snapshot.Targets;
            var count = 0;
            for (var i = 0; i < targets.Length; i++)
            {
                var near = targets.DistanceSq(i, centre) <= radius * radius;
                if (near && IsKnownEnemy(entities, turn, snapshot, i))
                {
                    count++;
                }
            }

            return count;
        }

        private readonly bool IsKnownEnemy(EntityManager entities, in AITurn turn, in AISnapshot snapshot, int index)
        {
            var targets = snapshot.Targets;
            var entity = targets.Entities[index];
            if (!_targetLookup.IsValidTarget(entity, turn.Owner, snapshot.Relations))
            {
                return false;
            }

            if (!turn.Settings.LimitedByFog)
            {
                return true;
            }

            var team = snapshot.Relations.GetTeam(turn.Owner);
            var building = entities.HasComponent<BuildingTag>(entity);
            return _fog.IsKnown(targets.Position(index), team, building, entities.HasComponent<TallTarget>(entity));
        }
    }
}
