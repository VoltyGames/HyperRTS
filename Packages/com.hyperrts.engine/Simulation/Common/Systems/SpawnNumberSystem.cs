using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common.Systems
{
    /// <summary>Gives each entity its <see cref="SpawnNumber"/> the first update it exists, before cues and replays read it.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup), OrderFirst = true)]
    public partial struct SpawnNumberSystem : ISystem
    {
        private int _next;

        [BurstCompile]
        public void OnCreate(ref SystemState state) => _next = 1;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Read-only scan, so a chunk's change version moves only when a number is given.
            var unnumbered = new NativeList<Entity>(Allocator.Temp);
            foreach (var (number, entity) in SystemAPI.Query<RefRO<SpawnNumber>>().WithEntityAccess())
            {
                if (number.ValueRO.Value == 0)
                {
                    unnumbered.Add(entity);
                }
            }

            foreach (var entity in unnumbered)
            {
                SystemAPI.SetComponent(entity, new SpawnNumber { Value = _next++ });
            }
        }
    }
}
