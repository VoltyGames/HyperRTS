using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Match;
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
        private EntityQuery _selected;
        private CargoExit _exit;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _selected = SystemAPI.QueryBuilder().WithAll<Container, Selected, Faction>().WithNone<Dead>().Build();
            _exit = new CargoExit(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!HasUnloadCommand(ref state))
            {
                return;
            }

            state.CompleteDependency();
            _exit.Update(ref state);
            var hasGrid = SystemAPI.TryGetSingleton<NavGrid>(out var grid) && grid.IsCreated;

            foreach (var (player, commands) in SystemAPI.Query<RefRO<Player>, DynamicBuffer<PlayerCommand>>())
            {
                foreach (var command in commands)
                {
                    if (command.Type != CommandType.Unload)
                    {
                        continue;
                    }

                    foreach (var container in CommandSubjects.Collect(command.Unit, _selected))
                    {
                        if (SystemAPI.HasComponent<Container>(container) &&
                            SystemAPI.GetComponent<Faction>(container).Value == player.ValueRO.Faction)
                        {
                            var freed = _exit.Unload(container, SystemAPI.GetBuffer<Cargo>(container), command.Argument,
                                hasGrid, grid);
                            SystemAPI.GetComponentRW<Container>(container).ValueRW.Used -= freed;
                        }
                    }
                }
            }
        }

        private bool HasUnloadCommand(ref SystemState state)
        {
            foreach (var commands in SystemAPI.Query<DynamicBuffer<PlayerCommand>>())
            {
                foreach (var command in commands)
                {
                    if (command.Type == CommandType.Unload)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
