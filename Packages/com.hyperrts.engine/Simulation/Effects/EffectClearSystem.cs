using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Effects
{
    /// <summary>
    /// Owns the <see cref="EffectQueue"/> in every world and empties it at the start of each frame, so a frame's
    /// events are played (or sent) once and never pile up.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct EffectClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var queue = state.EntityManager.CreateEntity(typeof(EffectQueue), typeof(EffectEvent));
            state.EntityManager.SetName(queue, "EffectQueue");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonBuffer<EffectEvent>().Clear();
        }
    }
}
