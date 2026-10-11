using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Spatial;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>
        /// Once enough idle combat units gather, sends them all at an enemy base along one of the map's attack lanes
        /// (see <see cref="SendAlongLane"/>). Units that can't hit the ground (air-to-air) stay home to defend.
        /// </summary>
        private void Attack(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var army = snapshot.Army;
            var wave = new NativeList<int>(Allocator.Temp);
            var center = GatherWave(ref state, turn.Faction, army, wave);
            var ai = SystemAPI.GetComponent<AIPlayer>(turn.Player);
            if (wave.Length == 0 || wave.Length < ai.AttackWaveSize)
            {
                return;
            }

            if (TryFindTarget(ref state, turn.Faction, center, ai.LimitedByFog, snapshot, out var target, out var enemy))
            {
                SendAlongLane(ref state, turn, snapshot, wave, center, target, enemy);
                return;
            }

            // Nothing known: sweep through the middle, where attack-move meets whatever is there.
            if (!ai.LimitedByFog || !SystemAPI.TryGetSingleton<MapSettings>(out var map))
            {
                return;
            }

            foreach (var i in wave)
            {
                turn.Commands.Add(AttackMoveCommand(army.Entities[i], map.Center, false));
            }
        }

        /// <summary>Adds the faction's idle units that can hit the ground to <paramref name="wave"/>; returns their centre.</summary>
        private float3 GatherWave(ref SystemState state, byte faction, in AIGroup army, NativeList<int> wave)
        {
            var sum = float3.zero;
            for (var i = 0; i < army.Length; i++)
            {
                var targets = SystemAPI.GetComponent<Weapon>(army.Entities[i]).Targets;
                if (army.IsOwnedBy(i, faction) && CombatMath.CanHit(targets, NavLayer.Ground))
                {
                    wave.Add(i);
                    sum += army.Position(i);
                }
            }

            return wave.Length > 0 ? sum / wave.Length : float3.zero;
        }

        /// <summary>
        /// Attack-moves the wave through the waypoints of one lane to <paramref name="enemy"/>'s base (starting at the
        /// one nearest the wave) and on to the <paramref name="target"/>, as shift-queued orders. The lane is a seeded
        /// choice among the map's lanes to that enemy, so bots, and one bot's successive attacks, take different routes.
        /// </summary>
        private void SendAlongLane(ref SystemState state, in Turn turn, in Snapshot snapshot, NativeList<int> wave, float3 center,
            float3 target, byte enemy)
        {
            var hints = snapshot.Hints;
            ref var ai = ref SystemAPI.GetComponentRW<AIPlayer>(turn.Player).ValueRW;
            var pick = AIRoutes.PickLane(ai.RouteSeed, turn.Faction, enemy, ai.Attacks,
                hints.LaneCount(turn.Faction, enemy));
            ai.Attacks++;
            var lane = hints.Lane(turn.Faction, enemy, pick);
            var first = hints.NearestWaypoint(lane, center.xz);
            var army = snapshot.Army;
            foreach (var i in wave)
            {
                var queue = false;
                for (var k = first; k < lane.Count; k++)
                {
                    var point = hints.Waypoint(lane, k);
                    turn.Commands.Add(AttackMoveCommand(army.Entities[i], new float3(point.x, 0f, point.y), queue));
                    queue = true;
                }

                turn.Commands.Add(AttackMoveCommand(army.Entities[i], target, queue));
            }
        }

        private static PlayerCommand AttackMoveCommand(Entity unit, float3 position, bool queue) => new()
        {
            Type = CommandType.AttackMove, Unit = unit, Position = position, Queue = queue,
        };

        /// <summary>
        /// Nearest hostile critical building, else the nearest hostile unit, among the enemies the map has a lane to
        /// from this faction (<paramref name="enemy"/> is the one found); undetected stealth is skipped, and with
        /// <paramref name="limitedByFog"/> so is what the team hasn't seen.
        /// </summary>
        private bool TryFindTarget(ref SystemState state, byte faction, float3 from, bool limitedByFog,
            in Snapshot snapshot, out float3 target, out byte enemy)
        {
            var team = snapshot.Relations.GetTeam(faction);
            var targets = snapshot.Targets;
            var reachable = snapshot.Hints.LaneEnemies(faction);
            var nearestBase = Closest.None;
            var nearestUnit = Closest.None;
            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                if (!IsAttackable(entity, targets.Owners[i].Value, faction, reachable, snapshot))
                {
                    continue;
                }

                var building = IsCriticalBuilding(ref state, entity);
                if (limitedByFog && !IsKnown(snapshot.Fog, targets.Position(i), team, building))
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
            target = best >= 0 ? targets.Position(best) : default;
            enemy = best >= 0 ? targets.Owners[best].Value : default;
            return best >= 0;
        }

        /// <summary>A valid target owned by an enemy in the <paramref name="reachable"/> set of factions.</summary>
        private bool IsAttackable(Entity entity, byte owner, byte faction, uint reachable, in Snapshot snapshot)
        {
            if ((Faction.Bit(owner) & reachable) == 0)
            {
                return false;
            }

            return _targetLookup.IsValidTarget(entity, faction, snapshot.Relations);
        }

        /// <summary>Units must be in sight; buildings stay known once explored, as they would on a human's map.</summary>
        private static bool IsKnown(in FogOfWar fog, float3 position, byte team, bool building)
        {
            if (!fog.IsCreated)
            {
                return true;
            }

            return building ? fog.IsExplored(position, team) : fog.IsVisible(position, team);
        }

        private bool IsCriticalBuilding(ref SystemState state, Entity entity) =>
            SystemAPI.HasComponent<BuildingTag>(entity) && SystemAPI.HasComponent<VictoryCritical>(entity);
    }
}
