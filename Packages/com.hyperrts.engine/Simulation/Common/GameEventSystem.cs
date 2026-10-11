using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Creates the <see cref="GameEvents"/> singleton and empties its buffers at the start of every frame.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct GameEventSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var events = state.EntityManager.CreateEntity(typeof(GameEvents), typeof(UnitProduced),
                typeof(BuildingCompleted), typeof(OwnerChanged));
            state.EntityManager.SetName(events, "GameEvents");
        }

        // Scheduled, so last frame's readers needn't finish on the main thread first.
        [BurstCompile]
        public void OnUpdate(ref SystemState state) => new ClearJob().Schedule();

        [BurstCompile]
        [WithAll(typeof(GameEvents))]
        private partial struct ClearJob : IJobEntity
        {
            private void Execute(DynamicBuffer<UnitProduced> produced, DynamicBuffer<BuildingCompleted> completed,
                DynamicBuffer<OwnerChanged> ownerChanges)
            {
                produced.Clear();
                completed.Clear();
                ownerChanges.Clear();
            }
        }
    }
}
