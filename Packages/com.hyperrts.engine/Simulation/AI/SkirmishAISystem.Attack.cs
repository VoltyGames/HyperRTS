using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
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

            if (!TryFindTarget(ref state, turn.Faction, center / wave.Length, snapshot, out var target))
            {
                return;
            }

            foreach (var i in wave)
            {
                turn.Commands.Add(new PlayerCommand
                {
                    Type = CommandType.AttackMove, Unit = army.Entities[i], Position = target,
                });
            }
        }

        /// <summary>Nearest hostile critical building, else the nearest hostile unit; undetected stealth is skipped.</summary>
        private bool TryFindTarget(ref SystemState state, byte faction, float3 from, in Snapshot snapshot,
            out float3 target)
        {
            var targets = snapshot.Targets;
            int bestBase = -1, bestUnit = -1;
            float bestBaseDistance = float.MaxValue, bestUnitDistance = float.MaxValue;
            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                if (!_targetLookup.IsValidTarget(entity, faction, snapshot.Relations))
                {
                    continue;
                }

                var distance = math.distancesq(targets.Position(i).xz, from.xz);
                if (IsCriticalBuilding(ref state, entity))
                {
                    if (IsCloser(distance, entity, bestBaseDistance, EntityAt(targets, bestBase)))
                    {
                        bestBase = i;
                        bestBaseDistance = distance;
                    }
                }
                else if (SystemAPI.HasComponent<UnitTag>(entity))
                {
                    if (IsCloser(distance, entity, bestUnitDistance, EntityAt(targets, bestUnit)))
                    {
                        bestUnit = i;
                        bestUnitDistance = distance;
                    }
                }
            }

            var best = bestBase >= 0 ? bestBase : bestUnit;
            target = best >= 0 ? targets.Position(best) : default;
            return best >= 0;
        }

        private static Entity EntityAt(in AIGroup group, int index) => index < 0 ? Entity.Null : group.Entities[index];

        private bool IsCriticalBuilding(ref SystemState state, Entity entity) =>
            SystemAPI.HasComponent<BuildingTag>(entity) && SystemAPI.HasComponent<VictoryCritical>(entity);
    }
}
