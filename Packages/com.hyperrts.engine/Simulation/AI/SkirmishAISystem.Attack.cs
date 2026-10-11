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
        /// Once enough idle combat units gather, attack-moves them all at the nearest enemy base. Units that can't hit
        /// the ground (air-to-air) stay home to defend.
        /// </summary>
        private void Attack(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var army = snapshot.Army;
            var wave = new NativeList<int>(Allocator.Temp);
            var center = float3.zero;
            for (var i = 0; i < army.Length; i++)
            {
                var targets = SystemAPI.GetComponent<Weapon>(army.Entities[i]).Targets;
                if (army.IsOwnedBy(i, turn.Faction) && CombatMath.CanHit(targets, NavLayer.Ground))
                {
                    wave.Add(i);
                    center += army.Position(i);
                }
            }

            var waveSize = SystemAPI.GetComponent<AIPlayer>(turn.Player).AttackWaveSize;
            if (wave.Length == 0 || wave.Length < waveSize)
            {
                return;
            }

            var limitedByFog = SystemAPI.GetComponent<AIPlayer>(turn.Player).LimitedByFog;
            if (!TryFindTarget(ref state, turn.Faction, center / wave.Length, limitedByFog, snapshot, out var target))
            {
                // Nothing known: sweep through the middle, where attack-move meets whatever is there.
                if (!limitedByFog || !SystemAPI.TryGetSingleton<MapSettings>(out var map))
                {
                    return;
                }

                target = map.Center;
            }

            foreach (var i in wave)
            {
                turn.Commands.Add(new PlayerCommand
                {
                    Type = CommandType.AttackMove, Unit = army.Entities[i], Position = target,
                });
            }
        }

        /// <summary>
        /// Nearest hostile critical building, else the nearest hostile unit; undetected stealth is skipped, and with
        /// <paramref name="limitedByFog"/> so is what the team hasn't seen.
        /// </summary>
        private bool TryFindTarget(ref SystemState state, byte faction, float3 from, bool limitedByFog,
            in Snapshot snapshot, out float3 target)
        {
            var team = snapshot.Relations.GetTeam(faction);
            var targets = snapshot.Targets;
            var nearestBase = Closest.None;
            var nearestUnit = Closest.None;
            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                if (!_targetLookup.IsValidTarget(entity, faction, snapshot.Relations))
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
            return best >= 0;
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
