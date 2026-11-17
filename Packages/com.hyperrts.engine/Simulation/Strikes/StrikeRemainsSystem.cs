using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Common.Prefabs;
using HyperRTS.Simulation.Common.Systems;
using Unity.Burst;
using Unity.Entities;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>Leaves each landing strike's <see cref="StrikeRemains"/> where it lands; a shot-down strike leaves none.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(ImpactSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct StrikeRemainsSystem : ISystem
    {
        private PrefabSpawner _spawner;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<StrikeRemains>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
            _spawner = new PrefabSpawner(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _spawner.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            foreach (var (projectile, remains, owner, entity) in SystemAPI
                         .Query<RefRO<Projectile>, RefRO<StrikeRemains>, RefRO<Owner>>().WithEntityAccess())
            {
                if (!projectile.ValueRO.Arrived || projectile.ValueRO.Intercepted)
                {
                    continue;
                }

                _spawner.Spawn(ecb, remains.ValueRO.Prefab, projectile.ValueRO.Hit.Position, owner.ValueRO.Value);
                ecb.RemoveComponent<StrikeRemains>(entity);
            }
        }
    }
}
