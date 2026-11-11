using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Transport
{
    /// <summary>
    /// When a container dies its passengers get out, or die with it when it doesn't let them survive. Runs before
    /// <see cref="DeathSystem"/> so they leave or die in the same frame, with kill credit to the container's killer.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    [UpdateBefore(typeof(DeathSystem))]
    public partial struct ContainerDeathSystem : ISystem
    {
        private CargoExit _exit;
        private EventWriter<EntityDamaged> _damaged;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _exit = new CargoExit(ref state);
            _damaged = new EventWriter<EntityDamaged>(ref state);
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _exit.Update(ref state);
            _damaged.Update(ref state);
            new EvacuateJob { Exit = _exit, Damaged = _damaged, Grid = SystemAPI.GetSingleton<NavGrid>() }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(Health))]
        [WithChangeFilter(typeof(Health))]
        private partial struct EvacuateJob : IJobEntity
        {
            public CargoExit Exit;
            public EventWriter<EntityDamaged> Damaged;
            [ReadOnly] public NavGrid Grid;

            private void Execute(Entity entity, in Container container, DynamicBuffer<Cargo> cargo)
            {
                if (cargo.IsEmpty || Exit.IsAlive(entity))
                {
                    return;
                }

                if (container.PassengersSurvive)
                {
                    Exit.Unload(entity, cargo, -1, Grid);
                }
                else
                {
                    Exit.KillAll(entity, cargo, ref Damaged);
                }
            }
        }
    }
}
