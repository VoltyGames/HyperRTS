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
            var known = default(NativeList<int>);
            for (var i = 0; i < _units.Length; i++)
            {
                if (!turn.IsFree(_units, i))
                {
                    continue;
                }

                var unit = _units.Entities[i];
                var primary = entities.GetBuffer<WeaponMount>(unit, true)[0];
                if (!primary.Ready || !primary.Weapon.FiresAtGround)
                {
                    continue;
                }

                if (!known.IsCreated)
                {
                    known = KnownEnemies(entities, turn, snapshot);
                }

                if (TryChoose(snapshot, known, _units.Position(i), primary.Weapon, out var point))
                {
                    turn.Commands.Add(new PlayerCommand { Type = CommandType.FireMission, Unit = unit, Position = point });
                    turn.Busy.Add(unit);
                }
            }
        }

        /// <summary>The snapshot indices of the enemies this AI may fire at, found once a turn.</summary>
        private readonly NativeList<int> KnownEnemies(EntityManager entities, in AITurn turn, in AISnapshot snapshot)
        {
            var known = new NativeList<int>(snapshot.Targets.Length, Allocator.Temp);
            for (var i = 0; i < snapshot.Targets.Length; i++)
            {
                if (IsKnownEnemy(entities, turn, snapshot, i))
                {
                    known.Add(i);
                }
            }

            return known;
        }

        /// <summary>The enemy in range with the most enemies within a blast of it, nearest on a tie; false when none is in range.</summary>
        private static bool TryChoose(in AISnapshot snapshot, in NativeList<int> known, float3 from, in Weapon weapon,
            out float3 point)
        {
            var targets = snapshot.Targets;
            var bestCount = 0;
            var bestDistance = float.MaxValue;
            point = default;
            foreach (var i in known)
            {
                var distance = targets.DistanceSq(i, from);
                if (!weapon.Reaches(math.sqrt(distance)))
                {
                    continue;
                }

                var count = CountWithin(snapshot, known, targets.Position(i), weapon.Blast);
                if (count > bestCount || (count == bestCount && distance < bestDistance))
                {
                    bestCount = count;
                    bestDistance = distance;
                    point = targets.Position(i);
                }
            }

            return bestCount > 0;
        }

        private static int CountWithin(in AISnapshot snapshot, in NativeList<int> known, float3 centre, float radius)
        {
            var count = 0;
            foreach (var i in known)
            {
                if (snapshot.Targets.DistanceSq(i, centre) <= radius * radius)
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
