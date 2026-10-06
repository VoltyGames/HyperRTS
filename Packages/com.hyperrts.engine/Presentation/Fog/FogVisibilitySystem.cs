using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>
    /// Mirrors <see cref="FogHidden"/> and passengers being <see cref="Inside"/> onto rendering; structural changes
    /// only on state flips.
    /// </summary>
    // The root's own DisableRendering records what was applied, so each job visits only roots that flipped.
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct FogVisibilitySystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var toggler = new RenderToggler
            {
                Linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
                Commands = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            };

            state.Dependency = new HideJob { Toggler = toggler }.ScheduleParallel(state.Dependency);
            state.Dependency = new RevealJob { Toggler = toggler }.ScheduleParallel(state.Dependency);
        }

        private struct RenderToggler
        {
            [ReadOnly] public BufferLookup<LinkedEntityGroup> Linked;
            [ReadOnly] public BufferLookup<Child> Children;
            public EntityCommandBuffer.ParallelWriter Commands;

            public void Set(int sortKey, Entity root, bool hide)
            {
                var targets = new FixedList512Bytes<Entity>();
                RenderHierarchy.Collect(root, Linked, Children, ref targets);
                foreach (var target in targets)
                {
                    if (hide)
                    {
                        Commands.AddComponent<DisableRendering>(sortKey, target);
                    }
                    else
                    {
                        Commands.RemoveComponent<DisableRendering>(sortKey, target);
                    }
                }
            }
        }

        // WithAny matches an enableable Inside only while it is enabled.
        [BurstCompile]
        [WithAll(typeof(EntityInfo))]
        [WithAny(typeof(FogHidden), typeof(Inside))]
        [WithNone(typeof(DisableRendering))]
        private partial struct HideJob : IJobEntity
        {
            public RenderToggler Toggler;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity) => Toggler.Set(sortKey, entity, true);
        }

        [BurstCompile]
        [WithAll(typeof(EntityInfo), typeof(DisableRendering))]
        [WithNone(typeof(FogHidden), typeof(Inside))]
        private partial struct RevealJob : IJobEntity
        {
            public RenderToggler Toggler;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity) => Toggler.Set(sortKey, entity, false);
        }
    }
}
