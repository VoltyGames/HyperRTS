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

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var events = SystemAPI.GetSingletonEntity<GameEvents>();
            SystemAPI.GetBuffer<UnitProduced>(events).Clear();
            SystemAPI.GetBuffer<BuildingCompleted>(events).Clear();
            SystemAPI.GetBuffer<OwnerChanged>(events).Clear();
        }
    }
}
