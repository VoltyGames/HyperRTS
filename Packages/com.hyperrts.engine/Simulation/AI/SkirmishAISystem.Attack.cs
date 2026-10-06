using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Units;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.AI
{
    public partial struct SkirmishAISystem
    {
        /// <summary>Once enough idle combat units gather, attack-moves them all at the nearest enemy base.</summary>
        private void Attack(ref SystemState state, in Turn turn, in Snapshot snapshot)
        {
            var army = snapshot.Army;
            var wave = new NativeList<int>(Allocator.Temp);
            var center = float3.zero;
            for (var i = 0; i < army.Length; i++)
            {
                if (army.IsOwnedBy(i, turn.Faction))
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
            var bestBase = float.MaxValue;
            var bestUnit = float.MaxValue;
            float3 basePosition = default, unitPosition = default;

            for (var i = 0; i < targets.Length; i++)
            {
                var entity = targets.Entities[i];
                var distance = math.distancesq(targets.Position(i).xz, from.xz);
                if (!snapshot.Relations.IsHostile(faction, targets.Owners[i].Value))
                {
                    continue;
                }

                if (_targetLookup.IsCloakedFrom(entity, snapshot.Relations.TeamOf(faction)))
                {
                    continue;
                }

                if (SystemAPI.HasComponent<BuildingTag>(entity) && SystemAPI.HasComponent<VictoryCritical>(entity) &&
                    distance < bestBase)
                {
                    bestBase = distance;
                    basePosition = targets.Position(i);
                }
                else if (SystemAPI.HasComponent<UnitTag>(entity) && distance < bestUnit)
                {
                    bestUnit = distance;
                    unitPosition = targets.Position(i);
                }
            }

            target = bestBase < float.MaxValue ? basePosition : unitPosition;
            return bestBase < float.MaxValue || bestUnit < float.MaxValue;
        }
    }
}
