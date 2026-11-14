using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// Handles Unload commands for the commanded or selected owned containers. A container in the air is called
    /// down first and lets its passengers out once it stands on the ground.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup))]
    public partial struct UnloadSystem : ISystem
    {
        private CommandReader _commands;
        private CargoExit _exit;
        private EntityQuery _requests;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var selected = SystemAPI.QueryBuilder().WithAll<Container, Selected, Owner>().WithNone<Dead>().Build();
            _commands = new CommandReader(ref state, TypeSet.Single(CommandType.Unload), selected);
            _exit = new CargoExit(ref state);
            _requests = SystemAPI.QueryBuilder().WithAll<UnloadRequest, Container>().Build();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var issued = _commands.Read(ref state);
            if (issued.IsEmpty && _requests.IsEmpty)
            {
                return;
            }

            state.CompleteDependency();
            foreach (var each in issued)
            {
                foreach (var container in _commands.Subjects(ref state, each))
                {
                    if (SystemAPI.HasComponent<Container>(container))
                    {
                        SystemAPI.SetComponent(container, new UnloadRequest { Slot = each.Command.Argument });
                        SystemAPI.SetComponentEnabled<UnloadRequest>(container, true);
                    }
                }
            }

            _exit.Update(ref state);
            var grid = SystemAPI.GetSingleton<NavGrid>();
            foreach (var (request, waiting, container) in SystemAPI
                         .Query<RefRO<UnloadRequest>, EnabledRefRW<UnloadRequest>>().WithAll<Container>()
                         .WithEntityAccess())
            {
                if (_exit.IsAloft(container, grid))
                {
                    SystemAPI.SetComponentEnabled<LandingCall>(container, true);
                    continue;
                }

                _exit.Unload(container, SystemAPI.GetBuffer<Cargo>(container), request.ValueRO.Slot, grid);
                waiting.ValueRW = false;
            }
        }
    }
}
