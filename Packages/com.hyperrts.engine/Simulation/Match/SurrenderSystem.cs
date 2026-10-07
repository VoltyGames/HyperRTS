using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Orders;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Defeats players who sent <see cref="CommandType.Surrender"/>; <see cref="VictorySystem"/> ends the match.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct SurrenderSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!PlayerCommands.Any(ref state, PlayerCommands.Mask(CommandType.Surrender)))
            {
                return;
            }

            foreach (var (commands, defeated) in SystemAPI.Query<DynamicBuffer<PlayerCommand>, EnabledRefRW<Defeated>>()
                         .WithPresent<Defeated>())
            {
                foreach (var command in commands)
                {
                    if (command.Type == CommandType.Surrender)
                    {
                        defeated.ValueRW = true;
                        break;
                    }
                }
            }
        }
    }
}
