using HyperRTS.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Advances <see cref="ConstructionProgress"/> (a 0..1 fraction) over time for
    /// every building still under construction. When a building reaches full
    /// progress the component is removed, marking the building complete.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ProductionSystemGroup))]
    public partial struct ConstructionSystem : ISystem
    {
        // Fraction of construction completed per second (~10 seconds to finish).
        private const float BuildRatePerSecond = 0.1f;

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (progress, entity) in
                     SystemAPI.Query<RefRW<ConstructionProgress>>()
                         .WithAll<BuildingTag>()
                         .WithEntityAccess())
            {
                var value = progress.ValueRO.Value + BuildRatePerSecond * deltaTime;

                if (value >= 1f)
                {
                    progress.ValueRW.Value = 1f;
                    ecb.RemoveComponent<ConstructionProgress>(entity);
                    continue;
                }

                progress.ValueRW.Value = value;
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
