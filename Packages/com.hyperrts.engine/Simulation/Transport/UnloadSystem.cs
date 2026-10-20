using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>Handles Unload commands for the commanded or selected owned containers.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct UnloadSystem : ISystem
    {
        private CommandReader _commands;
        private CargoExit _exit;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<Container, Selected, Faction>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Unload), selected);
            _exit = new CargoExit(ref state);
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            _exit.Update(ref state);
            var grid = SystemAPI.GetSingleton<NavGrid>();
            foreach (var each in issued)
            {
                foreach (var container in _commands.Subjects(ref state, each))
                {
                    if (SystemAPI.HasComponent<Container>(container))
                    {
                        _exit.Unload(container, SystemAPI.GetBuffer<Cargo>(container), each.Command.Argument, grid);
                    }
                }
            }
        }
    }
}
