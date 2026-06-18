using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Core.Health
{
    /// <summary>
    /// Destroys any entity whose <see cref="HealthComponent.CurrentHealth"/> has
    /// dropped to zero or below. Runs last in the simulation group so damage
    /// applied earlier in the frame is accounted for before entities are removed.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DeathSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (health, entity) in
                     SystemAPI.Query<RefRO<HealthComponent>>().WithEntityAccess())
            {
                if (health.ValueRO.CurrentHealth <= 0)
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
