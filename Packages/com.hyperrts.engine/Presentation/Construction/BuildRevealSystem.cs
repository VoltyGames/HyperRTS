using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Construction
{
    /// <summary>
    /// Copies a building's <see cref="ConstructionProgress"/> to <see cref="BuildProgress"/> on every mesh under it
    /// while it is under construction, and takes it off again once complete, so finished buildings draw as authored.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct BuildRevealSystem : ISystem
    {
        private EntityQuery _finished;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _finished = SystemAPI.QueryBuilder().WithAll<BuildRevealing>().WithDisabled<ConstructionProgress>().Build();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithPresent<ConstructionProgress>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
            var children = SystemAPI.GetBufferLookup<Child>(true);
            var meshes = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true);
            var parts = new NativeList<Entity>(Allocator.Temp);
            var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (progress, root) in SystemAPI.Query<RefRO<ConstructionProgress>>().WithNone<BuildRevealing>()
                         .WithEntityAccess())
            {
                RenderHierarchy.Collect(root, linked, children, parts);
                foreach (var part in parts)
                {
                    if (meshes.HasComponent(part))
                    {
                        commands.AddComponent(part, new BuildProgress { Value = progress.ValueRO.Value });
                    }
                }

                commands.AddComponent<BuildRevealing>(root);
            }

            foreach (var root in _finished.ToEntityArray(Allocator.Temp))
            {
                RenderHierarchy.Collect(root, linked, children, parts);
                foreach (var part in parts)
                {
                    commands.RemoveComponent<BuildProgress>(part);
                }

                commands.RemoveComponent<BuildRevealing>(root);
            }

            commands.Playback(state.EntityManager);
            Write(ref state, parts);
        }

        // After playback, so lookups see the meshes that just got the property.
        private void Write(ref SystemState state, NativeList<Entity> parts)
        {
            var linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
            var children = SystemAPI.GetBufferLookup<Child>(true);
            var shown = SystemAPI.GetComponentLookup<BuildProgress>();
            foreach (var (progress, root) in SystemAPI.Query<RefRO<ConstructionProgress>>().WithAll<BuildRevealing>()
                         .WithChangeFilter<ConstructionProgress>().WithEntityAccess())
            {
                RenderHierarchy.Collect(root, linked, children, parts);
                foreach (var part in parts)
                {
                    if (shown.HasComponent(part))
                    {
                        shown[part] = new BuildProgress { Value = progress.ValueRO.Value };
                    }
                }
            }
        }
    }
}
