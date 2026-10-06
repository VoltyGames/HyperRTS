using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Audio
{
    /// <summary>
    /// Owns the <see cref="SoundQueue"/> in every world and empties it at the start of each frame, so a frame's
    /// events are played (or sent) once and never pile up.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct SoundClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var queue = state.EntityManager.CreateEntity(typeof(SoundQueue), typeof(SoundEvent));
            state.EntityManager.SetName(queue, "SoundQueue");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonBuffer<SoundEvent>().Clear();
        }
    }
}
