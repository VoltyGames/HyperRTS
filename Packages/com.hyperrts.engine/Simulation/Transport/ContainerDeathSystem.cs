using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
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
        private NativeArray<byte> _noCells;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _exit = new CargoExit(ref state);
            _noCells = new NativeArray<byte>(1, Allocator.Persistent);
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) => _noCells.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _exit.Update(ref state);
            var hasGrid = SystemAPI.TryGetSingleton<NavGrid>(out var grid) && grid.IsCreated;
            new EvacuateJob
            {
                Exit = _exit,
                HasGrid = hasGrid,
                Grid = hasGrid ? grid : new NavGrid { Cells = _noCells },
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Dead))]
        private partial struct EvacuateJob : IJobEntity
        {
            public CargoExit Exit;
            public bool HasGrid;
            [ReadOnly] public NavGrid Grid;

            private void Execute(Entity entity, ref Container container, DynamicBuffer<Cargo> cargo)
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

                container.Used = 0;
            }
        }
    }
}
