using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Counts down <see cref="Decay"/> remains, sinks them once their lifetime runs out and destroys them sunk.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DecaySystem : ISystem
    {
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            state.RequireForUpdate<Decay>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _groups.Update(ref state);
            _children.Update(ref state);
            var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged);
            new DecayJob
            {
                Ecb = ecb.AsParallelWriter(), DeltaTime = SystemAPI.Time.DeltaTime, Groups = _groups,
                Children = _children,
            }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct DecayJob : IJobEntity
        {
            public EntityCommandBuffer.ParallelWriter Ecb;
            public float DeltaTime;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public BufferLookup<Child> Children;

            // The transform is written only while sinking, so a waiting ghost sends no changes.
            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref Decay decay,
                RefRW<LocalTransform> transform)
            {
                var sinkTime = DeltaTime;
                if (decay.Lifetime > 0f)
                {
                    decay.Lifetime -= DeltaTime;
                    if (decay.Lifetime > 0f)
                    {
                        return;
                    }

                    // The tick that ends the lifetime sinks only by what it overran.
                    sinkTime = -decay.Lifetime;
                }

                var sunk = math.min(1f, decay.Sunk + sinkTime / decay.SinkDuration);
                transform.ValueRW.Position.y -= (sunk - decay.Sunk) * decay.SinkDepth;
                decay.Sunk = sunk;
                if (sunk < 1f)
                {
                    return;
                }

                var parts = new NativeList<Entity>(4, Allocator.Temp);
                EntityParts.Collect(entity, Groups, Children, parts);
                Ecb.DestroyEntity(sortKey, parts.AsArray());
            }
        }
    }
}
