using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Hides hostile units and buildings the local team can't see; structural changes only on state flips.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct FogVisibilitySystem : ISystem
    {
        // Last evaluated state; the fog grid restamps at ~10 Hz and Faction's order version covers spawns.
        private int _fogVersion;
        private int _factionOrder;
        private byte _viewer;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BeginPresentationEntityCommandBufferSystem.Singleton>();
            state.RequireForUpdate<LocalFogView>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var view = SystemAPI.GetSingleton<LocalFogView>();
            var factionOrder = state.EntityManager.GetComponentOrderVersion<Faction>();
            if (view.Active && view.Fog.Version == _fogVersion && factionOrder == _factionOrder &&
                view.Viewer == _viewer)
            {
                return;
            }

            // A zero fog version never matches a built grid, so re-enabling fog re-evaluates everything.
            _fogVersion = view.Active ? view.Fog.Version : 0;
            _factionOrder = factionOrder;
            _viewer = view.Viewer;
            var toggler = new RenderToggler
            {
                Linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true),
                Children = SystemAPI.GetBufferLookup<Child>(true),
                Commands = SystemAPI.GetSingleton<BeginPresentationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            };

            state.Dependency = view.Active
                ? new HideJob
                {
                    Toggler = toggler,
                    View = view,
                    Hidden = SystemAPI.GetComponentLookup<FogHidden>(true),
                }.ScheduleParallel(state.Dependency)
                : new RevealAllJob { Toggler = toggler }.ScheduleParallel(state.Dependency);
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

                if (hide)
                {
                    Commands.AddComponent<FogHidden>(sortKey, root);
                }
                else
                {
                    Commands.RemoveComponent<FogHidden>(sortKey, root);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(EntityInfo))]
        private partial struct HideJob : IJobEntity
        {
            public RenderToggler Toggler;

            [ReadOnly] public LocalFogView View;
            [ReadOnly] public ComponentLookup<FogHidden> Hidden;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, in Faction faction, in LocalToWorld transform)
            {
                var hide = View.IsHidden(faction.Value, transform.Position);
                if (hide != Hidden.HasComponent(entity))
                {
                    Toggler.Set(sortKey, entity, hide);
                }
            }
        }

        [BurstCompile]
        [WithAll(typeof(FogHidden))]
        private partial struct RevealAllJob : IJobEntity
        {
            public RenderToggler Toggler;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity)
            {
                Toggler.Set(sortKey, entity, false);
            }
        }
    }
}
