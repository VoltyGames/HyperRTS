using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Death;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Strikes
{
    /// <summary>
    /// Removes the remains lying within each <see cref="ClearsRemains"/> radius, once: the frame after the strike landed,
    /// when the remains of what its blast killed exist. Later deaths there leave theirs.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    public partial struct RemainsClearingSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<ClearsRemains>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            var zones = new NativeList<float3>(4, state.WorldUpdateAllocator);
            foreach (var (clears, transform, entity) in SystemAPI.Query<RefRO<ClearsRemains>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                zones.Add(new float3(transform.ValueRO.Position.xz, clears.ValueRO.Radius));
                ecb.RemoveComponent<ClearsRemains>(entity);
            }

            new ClearJob { Ecb = ecb, Zones = zones }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(RemainsTag))]
        [WithNone(typeof(SceneryCleared))]
        private partial struct ClearJob : IJobEntity
        {
            public EntityCommandBuffer Ecb;

            /// <summary>Each zone's centre in xy and its radius in z.</summary>
            [ReadOnly] public NativeList<float3> Zones;

            private void Execute(Entity entity, in LocalTransform transform)
            {
                foreach (var zone in Zones)
                {
                    if (math.distance(zone.xy, transform.Position.xz) < zone.z)
                    {
                        Ecb.AddComponent<SceneryCleared>(entity);
                        return;
                    }
                }
            }
        }
    }
}
