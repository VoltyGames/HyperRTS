using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources.Gathering;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.AI
{
    /// <summary>
    /// Once enough idle combat units gather, sends them all at an enemy base along one of the map's attack lanes
    /// (see <see cref="SendAlongLane"/>). Units that can't hit the ground (air-to-air) stay home to defend.
    /// </summary>
    internal struct AttackPlanner
    {
        private EntityQuery _armyQuery;
        private AIGroup _army;
        private TargetLookup _targetLookup;
        private TargetQueries _targetQueries;
        private SquadPlanner _squads;

        /// <summary>Default (not created) in a match without fog, where everything counts as seen.</summary>
        private FogOfWar _fog;

        public static AttackPlanner Create(ref SystemState state) => new()
        {
            _armyQuery = new EntityQueryBuilder(Allocator.Temp).WithAll<WeaponMount, CombatStance, Owner, LocalTransform>()
                .WithDisabled<ActiveOrder>().WithNone<Harvester, Dead, AttackTarget>().Build(ref state),
            _targetLookup = new TargetLookup(ref state),
            _targetQueries = TargetQueries.Create(ref state),
        };

        public void Update(ref SystemState state, in SquadPlanner squads)
        {
            _targetLookup.Update(ref state, _targetQueries);
            _army = AIGroup.From(_armyQuery);
            _fog = _targetQueries.Fog.TryGetSingleton(out FogOfWar fog) ? fog : default;
            _squads = squads;
        }

        public void Plan(ref SystemState state, in AITurn turn, in AISnapshot snapshot)
        {
            var wave = new NativeList<int>(Allocator.Temp);
            var center = GatherWave(ref state, turn, snapshot, wave);
            var ai = turn.Settings;
            if (wave.Length == 0 || wave.Length < ai.AttackWaveSize)
            {
                return;
            }

            if (TryFindTarget(ref state, turn, snapshot, center, ai.LimitedByFog, out var goal))
            {
                SendAlongLane(ref state, turn, snapshot, wave, center, goal);
                return;
            }

            // Nothing known: sweep through the middle, where attack-move meets whatever is there.
            if (!ai.LimitedByFog)
            {
                return;
            }

            turn.Commands.Add(WaveCommand(turn, wave, snapshot.Map.Center));
        }

        /// <summary>One attack-move addressing the whole wave, so it moves as a formation on one group field.</summary>
        private readonly PlayerCommand WaveCommand(in AITurn turn, NativeList<int> wave, float3 position)
        {
            var units = new NativeArray<Entity>(wave.Length, Allocator.Temp);
            for (var k = 0; k < wave.Length; k++)
            {
                units[k] = _army.Entities[wave[k]];
            }

            var command = new PlayerCommand { Type = CommandType.AttackMove, Position = position };
            PlayerCommands.Address(ref command, turn.Subjects, units);
            return command;
        }

        /// <summary>
        /// Adds the owner's idle units that can hit the ground to <paramref name="wave"/>; returns their centre.
        /// Builders other than squads stay out, and so do units given a job this think and squads still recovering.
        /// </summary>
        private readonly float3 GatherWave(ref SystemState state, in AITurn turn, in AISnapshot snapshot,
            NativeList<int> wave)
        {
            var entities = state.EntityManager;
            var sum = float3.zero;
            for (var i = 0; i < _army.Length; i++)
            {
                if (!_army.IsOwnedBy(i, turn.Owner))
                {
                    continue;
                }

                var entity = _army.Entities[i];
                var armed = WeaponMount.TryGetPrimary(entities.GetBuffer<WeaponMount>(entity, true), out var primary);
                if (armed && Weapon.CanHit(primary.Targets, NavLayer.Ground)
                    && IsFreeForWave(ref state, turn, snapshot, entity))
                {
                    wave.Add(i);
                    sum += _army.Position(i);
                }
            }

            return wave.Length > 0 ? sum / wave.Length : float3.zero;
        }

        private readonly bool IsFreeForWave(ref SystemState state, in AITurn turn, in AISnapshot snapshot, Entity unit)
        {
            var entities = state.EntityManager;
            var isSquad = entities.HasBuffer<SquadMember>(unit);
            if (entities.HasComponent<Builder>(unit) && !isSquad)
            {
                return false;
            }

            return !turn.Busy.Contains(unit) && !_squads.IsRecovering(ref state, turn, snapshot, unit);
        }

        /// <summary>
        /// Attack-moves the wave along a seeded lane to the goal's enemy base and on to the goal, so routes vary
        /// between bots and attacks.
        /// </summary>
        private readonly void SendAlongLane(ref SystemState state, in AITurn turn, in AISnapshot snapshot,
            NativeList<int> wave, float3 center, in Goal goal)
        {
            var hints = snapshot.Hints;
            var entities = state.EntityManager;
            var ai = entities.GetComponentData<AIPlayer>(turn.Player);
            var pick = AIRoutes.PickLane(ai.RouteSeed, turn.Owner, goal.Enemy, ai.Attacks,
                hints.LaneCount(turn.Owner, goal.Enemy));
            ai.Attacks++;
            entities.SetComponentData(turn.Player, ai);
            var lane = hints.Lane(turn.Owner, goal.Enemy, pick);
            var first = hints.NearestWaypoint(lane, center.xz);
            var command = WaveCommand(turn, wave, goal.Position);
            for (var k = first; k < lane.Count; k++)
            {
                var point = hints.Waypoint(lane, k);
                turn.Commands.Add(Leg(command, new float3(point.x, 0f, point.y), k > first));
            }

            turn.Commands.Add(Leg(command, goal.Position, lane.Count > first));
        }

        private static PlayerCommand Leg(PlayerCommand wave, float3 position, bool queue)
        {
            wave.Position = position;
            wave.Queue = queue;
            return wave;
        }

        /// <summary>
        /// Nearest hostile critical building, else the nearest hostile unit, among enemies the map has a lane to;
        /// undetected stealth is skipped, and with <paramref name="limitedByFog"/> so is what the team hasn't seen.
        /// </summary>
        private bool TryFindTarget(ref SystemState state, in AITurn turn, in AISnapshot snapshot, float3 from,
            bool limitedByFog, out Goal goal)
        {
            var entities = state.EntityManager;
            var team = snapshot.Relations.GetTeam(turn.Owner);
            var targets = snapshot.Targets;
            var reachable = snapshot.Hints.LaneEnemies(turn.Owner);
            var nearestBase = Closest.None;
            var nearestUnit = Closest.None;
            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                if (!IsAttackable(entity, targets.Owners[i].Value, turn.Owner, reachable, snapshot))
                {
                    continue;
                }

                var building = entities.HasComponent<BuildingTag>(entity) && entities.HasComponent<VictoryCritical>(entity);
                var tall = entities.HasComponent<TallTarget>(entity);
                if (limitedByFog && !IsKnown(targets.Position(i), team, building, tall))
                {
                    continue;
                }

                var distance = targets.DistanceSq(i, from);
                if (building)
                {
                    nearestBase.Consider(i, entity, distance);
                }
                else if (entities.HasComponent<UnitTag>(entity))
                {
                    nearestUnit.Consider(i, entity, distance);
                }
            }

            var best = nearestBase.Found ? nearestBase.Index : nearestUnit.Index;
            goal = best >= 0 ? new Goal(targets.Position(best), targets.Owners[best].Value) : default;
            return best >= 0;
        }

        /// <summary>A valid target owned by an enemy in the <paramref name="reachable"/> set of owners.</summary>
        private bool IsAttackable(Entity entity, byte targetOwner, byte owner, uint reachable, in AISnapshot snapshot)
        {
            if ((Owner.Bit(targetOwner) & reachable) == 0)
            {
                return false;
            }

            return _targetLookup.IsValidTarget(entity, owner, snapshot.Relations);
        }

        /// <summary>Units must be in sight; buildings stay known once explored, as they would on a human's map.</summary>
        private readonly bool IsKnown(float3 position, byte team, bool building, bool tall)
        {
            if (!_fog.IsCreated)
            {
                return true;
            }

            return building ? _fog.IsExplored(position, team) : _fog.IsVisible(position, team, tall);
        }

        /// <summary>Where an attack heads and which enemy owns it.</summary>
        private readonly struct Goal
        {
            public readonly float3 Position;
            public readonly byte Enemy;

            public Goal(float3 position, byte enemy)
            {
                Position = position;
                Enemy = enemy;
            }
        }
    }
}
