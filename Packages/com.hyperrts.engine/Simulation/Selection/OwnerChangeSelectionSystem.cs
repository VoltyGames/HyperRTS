using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Common.Events;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>
    /// Deselects an entity that changed hands (<see cref="OwnerChanged"/>). Last in the phase, after every system that
    /// hands entities over.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderLast = true)]
    public partial struct OwnerChangeSelectionSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GameEvents>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new DeselectJob { Selected = SystemAPI.GetComponentLookup<Selected>(false) }.Schedule();
        }

        /// <summary>Single-threaded: it writes the changed entities, not the event holder it visits.</summary>
        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct DeselectJob : IJobEntity
        {
            public ComponentLookup<Selected> Selected;

            private void Execute(in DynamicBuffer<OwnerChanged> changes)
            {
                foreach (var change in changes)
                {
                    if (Selected.HasComponent(change.Entity))
                    {
                        Selected.SetComponentEnabled(change.Entity, false);
                    }
                }
            }
        }
    }
}
