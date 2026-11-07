using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Infantry;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Vision;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
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

        /// <summary>
        /// Once enough idle combat units gather, sends them all at an enemy base along one of the map's attack lanes
        /// (see <see cref="SendAlongLane"/>). Units that can't hit the ground (air-to-air) stay home to defend.
        /// </summary>
        private void Attack(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var army = snapshot.Army;
            var wave = new NativeList<int>(Allocator.Temp);
            var center = GatherWave(ref state, turn, snapshot, wave);
            var ai = SystemAPI.GetComponent<AIPlayer>(turn.Player);
            if (wave.Length == 0 || wave.Length < ai.AttackWaveSize)
            {
                return;
            }

            if (TryFindTarget(ref state, turn.Owner, center, ai.LimitedByFog, snapshot, out var goal))
            {
                SendAlongLane(ref state, turn, snapshot, wave, center, goal);
                return;
            }

            // Nothing known: sweep through the middle, where attack-move meets whatever is there.
            if (!ai.LimitedByFog)
            {
                return;
            }

            var map = SystemAPI.GetSingleton<MapSettings>();
            turn.Commands.Add(WaveCommand(turn, army, wave, map.Center));
        }

        /// <summary>One attack-move addressing the whole wave, so it moves as a formation on one group field.</summary>
        private static PlayerCommand WaveCommand(in Turn turn, in AIGroup army, NativeList<int> wave, float3 position)
        {
            var units = new NativeArray<Entity>(wave.Length, Allocator.Temp);
            for (var k = 0; k < wave.Length; k++)
            {
                units[k] = army.Entities[wave[k]];
            }

            var command = new PlayerCommand { Type = CommandType.AttackMove, Position = position };
            PlayerCommands.Address(ref command, turn.Subjects, units);
            return command;
        }

        /// <summary>
        /// Adds the owner's idle units that can hit the ground to <paramref name="wave"/>; returns their centre.
        /// Builders other than squads stay out, and so do units given a job this think and squads still recovering.
        /// </summary>
        private float3 GatherWave(ref SystemState state, in Turn turn, in Snapshot snapshot, NativeList<int> wave)
        {
            var sum = float3.zero;
            var army = snapshot.Army;
            var mounts = SystemAPI.GetBufferLookup<WeaponMount>(true);
            for (var i = 0; i < army.Length; i++)
            {
                var entity = army.Entities[i];
                var armed = WeaponMount.TryGetPrimary(mounts[entity], out var primary);
                if (armed && army.IsOwnedBy(i, turn.Owner) && Weapon.CanHit(primary.Targets, NavLayer.Ground)
                    && IsFreeForWave(ref state, entity, turn, snapshot))
                {
                    wave.Add(i);
                    sum += army.Position(i);
                }
            }

            return wave.Length > 0 ? sum / wave.Length : float3.zero;
        }

        private bool IsFreeForWave(ref SystemState state, Entity unit, in Turn turn, in Snapshot snapshot)
        {
            var isSquad = SystemAPI.HasBuffer<SquadMember>(unit);
            if (SystemAPI.HasComponent<Builder>(unit) && !isSquad)
            {
                return false;
            }

            return !turn.Busy.Contains(unit) && !IsRecovering(ref state, unit, turn, snapshot);
        }

        /// <summary>
        /// Attack-moves the wave along a seeded lane to <paramref name="enemy"/>'s base and on to the <paramref
        /// name="target"/>, so routes vary between bots and attacks.
        /// </summary>
        private void SendAlongLane(ref SystemState state, in Turn turn, in Snapshot snapshot, NativeList<int> wave, float3 center,
            in Goal goal)
        {
            var hints = snapshot.Hints;
            var target = goal.Position;
            var enemy = goal.Enemy;
            ref var ai = ref SystemAPI.GetComponentRW<AIPlayer>(turn.Player).ValueRW;
            var pick = AIRoutes.PickLane(ai.RouteSeed, turn.Owner, enemy, ai.Attacks,
                hints.LaneCount(turn.Owner, enemy));
            ai.Attacks++;
            var lane = hints.Lane(turn.Owner, enemy, pick);
            var first = hints.NearestWaypoint(lane, center.xz);
            var command = WaveCommand(turn, snapshot.Army, wave, target);
            for (var k = first; k < lane.Count; k++)
            {
                var point = hints.Waypoint(lane, k);
                turn.Commands.Add(Leg(command, new float3(point.x, 0f, point.y), k > first));
            }

            turn.Commands.Add(Leg(command, target, lane.Count > first));
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
        private bool TryFindTarget(ref SystemState state, byte owner, float3 from, bool limitedByFog,
            in Snapshot snapshot, out Goal goal)
        {
            var team = snapshot.Relations.GetTeam(owner);
            var targets = snapshot.Targets;
            var reachable = snapshot.Hints.LaneEnemies(owner);
            var nearestBase = Closest.None;
            var nearestUnit = Closest.None;
            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                if (!IsAttackable(entity, targets.Owners[i].Value, owner, reachable, snapshot))
                {
                    continue;
                }

                var building = IsCriticalBuilding(ref state, entity);
                var tall = SystemAPI.HasComponent<TallTarget>(entity);
                if (limitedByFog && !IsKnown(snapshot.Fog, targets.Position(i), team, building, tall))
                {
                    continue;
                }

                var distance = targets.DistanceSq(i, from);
                if (building)
                {
                    nearestBase.Consider(i, entity, distance);
                }
                else if (SystemAPI.HasComponent<UnitTag>(entity))
                {
                    nearestUnit.Consider(i, entity, distance);
                }
            }

            var best = nearestBase.Found ? nearestBase.Index : nearestUnit.Index;
            goal = best >= 0 ? new Goal(targets.Position(best), targets.Owners[best].Value) : default;
            return best >= 0;
        }

        /// <summary>A valid target owned by an enemy in the <paramref name="reachable"/> set of owners.</summary>
        private bool IsAttackable(Entity entity, byte targetOwner, byte owner, uint reachable, in Snapshot snapshot)
        {
            if ((Owner.Bit(targetOwner) & reachable) == 0)
            {
                return false;
            }

            return _targetLookup.IsValidTarget(entity, owner, snapshot.Relations);
        }

        /// <summary>Units must be in sight; buildings stay known once explored, as they would on a human's map.</summary>
        private static bool IsKnown(in FogOfWar fog, float3 position, byte team, bool building, bool tall)
        {
            if (!fog.IsCreated)
            {
                return true;
            }

            return building ? fog.IsExplored(position, team) : fog.IsVisible(position, team, tall);
        }

        private bool IsCriticalBuilding(ref SystemState state, Entity entity) =>
            SystemAPI.HasComponent<BuildingTag>(entity) && SystemAPI.HasComponent<VictoryCritical>(entity);
    }
}
