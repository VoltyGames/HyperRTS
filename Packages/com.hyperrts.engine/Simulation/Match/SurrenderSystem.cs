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
        private CommandReader _commands;

        [BurstCompile]
        public void OnCreate(ref SystemState state) =>
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Surrender));

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var each in _commands.Read(ref state))
            {
                SystemAPI.SetComponentEnabled<Defeated>(each.Player, true);
            }
        }
    }
}
