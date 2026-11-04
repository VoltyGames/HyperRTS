using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Vision.Sight;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat.Death
{
    /// <summary>
    /// Starts <see cref="Decay"/> remains' clocks, buries them when their lifetime ends (no longer blocking movement or
    /// sight), then destroys them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DecaySystem : ISystem
    {

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Decay>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        // Waiting remains are only read, so their chunks keep their change versions and static ghosts stay quiet.
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var now = SystemAPI.Time.ElapsedTime;
            var due = new NativeQueue<Entity>(Allocator.TempJob);
            new FindDueJob { Due = due.AsParallelWriter(), Now = now }.ScheduleParallel();
            state.Dependency = new DespawnJob
            {
                Due = due, Now = now, Decays = SystemAPI.GetComponentLookup<Decay>(false), Transforms = SystemAPI.GetComponentLookup<LocalTransform>(false), Blockers = SystemAPI.GetComponentLookup<Blocker>(false),
                SightBlocks = SystemAPI.GetComponentLookup<SightBlock>(false),
                Groups = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true), Children = SystemAPI.GetBufferLookup<Child>(true),
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            }.Schedule(state.Dependency);
            due.Dispose(state.Dependency);
        }

        [BurstCompile]
        [WithAll(typeof(LocalTransform))]
        private partial struct FindDueJob : IJobEntity
        {
            public NativeQueue<Entity>.ParallelWriter Due;
            public double Now;

            private void Execute(Entity entity, in Decay decay)
            {
                var unstarted = decay.DespawnStartTime == 0d;
                if (unstarted || Now >= decay.DespawnStartTime)
                {
                    Due.Enqueue(entity);
                }
            }
        }

        [BurstCompile]
        private struct DespawnJob : IJob
        {
            public NativeQueue<Entity> Due;
            public double Now;
            public ComponentLookup<Decay> Decays;
            public ComponentLookup<LocalTransform> Transforms;
            public ComponentLookup<Blocker> Blockers;
            public ComponentLookup<SightBlock> SightBlocks;
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public BufferLookup<Child> Children;
            public EntityCommandBuffer.ParallelWriter Ecb;

            public void Execute()
            {
                while (Due.TryDequeue(out var entity))
                {
                    Advance(entity);
                }
            }

            private void Advance(Entity entity)
            {
                var decay = Decays.GetRefRW(entity);
                ref var value = ref decay.ValueRW;
                if (value.DespawnStartTime == 0d)
                {
                    value.DespawnStartTime = Now + value.Lifetime;
                    return;
                }

                // Remains being buried are on their way out, so units may drive over and see past them.
                Blockers.DisableIfPresent(entity);
                SightBlocks.DisableIfPresent(entity);

                var progress = (float)math.min(1d, (Now - value.DespawnStartTime) / value.DespawnDuration);
                Transforms.GetRefRW(entity).ValueRW.Position.y -= (progress - value.DespawnProgress) * value.DespawnDepth;
                value.DespawnProgress = progress;
                if (progress < 1f)
                {
                    return;
                }

                EntityParts.Destroy(entity, Groups, Children, Ecb, 0);
            }
        }
    }
}
