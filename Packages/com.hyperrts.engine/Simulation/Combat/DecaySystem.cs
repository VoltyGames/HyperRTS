using HyperRTS.Core;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Starts <see cref="Decay"/> remains' clocks, buries them when their lifetime ends (no longer blocking), then destroys
    /// them.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(LifecycleSystemGroup))]
    public partial struct DecaySystem : ISystem
    {
        private ComponentLookup<Decay> _decays;
        private ComponentLookup<LocalTransform> _transforms;
        private ComponentLookup<Blocker> _blockers;
        private BufferLookup<LinkedEntityGroup> _groups;
        private BufferLookup<Child> _children;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _decays = state.GetComponentLookup<Decay>();
            _transforms = state.GetComponentLookup<LocalTransform>();
            _blockers = state.GetComponentLookup<Blocker>();
            _groups = state.GetBufferLookup<LinkedEntityGroup>(true);
            _children = state.GetBufferLookup<Child>(true);
            state.RequireForUpdate<Decay>();
            state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        }

        // Waiting remains are only read, so their chunks keep their change versions and static ghosts stay quiet.
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _decays.Update(ref state);
            _transforms.Update(ref state);
            _blockers.Update(ref state);
            _groups.Update(ref state);
            _children.Update(ref state);
            var now = SystemAPI.Time.ElapsedTime;
            var due = new NativeQueue<Entity>(Allocator.TempJob);
            new FindDueJob { Due = due.AsParallelWriter(), Now = now }.ScheduleParallel();
            state.Dependency = new DespawnJob
            {
                Due = due, Now = now, Decays = _decays, Transforms = _transforms, Blockers = _blockers,
                Groups = _groups, Children = _children,
                Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged),
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
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Groups;
            [ReadOnly] public BufferLookup<Child> Children;
            public EntityCommandBuffer Ecb;

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

                // Remains being buried are on their way out, so units may drive over them.
                if (Blockers.HasComponent(entity))
                {
                    Blockers.SetComponentEnabled(entity, false);
                }

                var progress = (float)math.min(1d, (Now - value.DespawnStartTime) / value.DespawnDuration);
                Transforms.GetRefRW(entity).ValueRW.Position.y -= (progress - value.DespawnProgress) * value.DespawnDepth;
                value.DespawnProgress = progress;
                if (progress < 1f)
                {
                    return;
                }

                var parts = new NativeList<Entity>(4, Allocator.Temp);
                EntityParts.Collect(entity, Groups, Children, parts);
                Ecb.DestroyEntity(parts.AsArray());
            }
        }
    }
}
