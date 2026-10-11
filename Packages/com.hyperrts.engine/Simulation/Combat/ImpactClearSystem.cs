using HyperRTS.Core;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Owns the <see cref="ImpactQueue"/> in every world and empties it at the start of each frame, so a frame's
    /// impacts are drawn (or sent, or recorded) once. Replay playback refills it while the gameplay phases are off.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.All)]
    [UpdateInGroup(typeof(OrderSystemGroup), OrderFirst = true)]
    public partial struct ImpactClearSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var queue = state.EntityManager.CreateEntity(typeof(ImpactQueue), typeof(ImpactEvent));
            state.EntityManager.SetName(queue, "ImpactQueue");
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.GetSingletonBuffer<ImpactEvent>().Clear();
        }
    }
}
