using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>When a container dies its passengers get out, or die with it when it doesn't let them survive.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateAfter(typeof(DeathSystem))]
    public partial struct ContainerDeathSystem : ISystem
    {
        private CargoExit _exit;
        private NavGrid _noGrid;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _exit = new CargoExit(ref state);
            _noGrid = NavGrid.Placeholder();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _noGrid.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _exit.Update(ref state);
            var hasGrid = SystemAPI.TryGetSingleton<NavGrid>(out var grid) && grid.IsCreated;
            new EvacuateJob
            {
                Exit = _exit,
                HasGrid = hasGrid,
                Grid = hasGrid ? grid : _noGrid,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Dead))]
        private partial struct EvacuateJob : IJobEntity
        {
            public CargoExit Exit;
            public bool HasGrid;
            [ReadOnly] public NavGrid Grid;

            private void Execute(Entity entity, in Container container, DynamicBuffer<Cargo> cargo)
            {
                if (cargo.IsEmpty)
                {
                    return;
                }

                if (container.PassengersSurvive)
                {
                    Exit.Unload(entity, cargo, -1, HasGrid, Grid);
                }
                else
                {
                    Exit.KillAll(entity, cargo);
                }
            }
        }
    }
}
