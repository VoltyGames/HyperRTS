using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Owns the <see cref="CueQueue"/> in every world and empties it at the start of each frame, so a frame's
    /// events are played (or sent) once and never pile up.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct CueClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var queue = state.EntityManager.CreateEntity(typeof(CueQueue), typeof(CueEvent));
            state.EntityManager.SetName(queue, "CueQueue");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonBuffer<CueEvent>().Clear();
        }
    }
}
