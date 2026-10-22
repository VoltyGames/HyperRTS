using HyperRTS.Core;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.TeamColors
{
    /// <summary>
    /// Writes each building's <see cref="BuildingLights"/> to <see cref="LightsOn"/> on every mesh under it: dark while
    /// <see cref="Offline"/> (under construction included), lit otherwise, with a flicker on each change. The
    /// <see cref="ConstructionSite"/> dressing keeps its own work lights on.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct BuildingLightsSystem : ISystem
    {
        private EntityQuery _unseen;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _unseen = SystemAPI.QueryBuilder().WithAll<BuildingTag>().WithPresent<Offline>().WithNone<BuildingLights>()
                .Build();
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BuildingTag>().WithPresent<Offline>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            var linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
            var children = SystemAPI.GetBufferLookup<Child>(true);
            var meshes = SystemAPI.GetComponentLookup<MaterialMeshInfo>(true);
            var sites = SystemAPI.GetComponentLookup<ConstructionSite>(true);
            var parts = new NativeList<Entity>(Allocator.Temp);
            var site = new NativeList<Entity>(Allocator.Temp);
            var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach (var root in _unseen.ToEntityArray(Allocator.Temp))
            {
                var lights = BuildingLights.Seen(!SystemAPI.IsComponentEnabled<Offline>(root));
                CollectLit(root, sites, linked, children, parts, site);
                foreach (var part in parts)
                {
                    if (meshes.HasComponent(part))
                    {
                        commands.AddComponent(part, new LightsOn { Value = lights.Value });
                    }
                }

                commands.AddComponent(root, lights);
            }

            commands.Playback(state.EntityManager);
            Write(ref state, parts, site);
        }

        // After playback, so lookups see the meshes that just got the property.
        private void Write(ref SystemState state, NativeList<Entity> parts, NativeList<Entity> site)
        {
            var linked = SystemAPI.GetBufferLookup<LinkedEntityGroup>(true);
            var children = SystemAPI.GetBufferLookup<Child>(true);
            var sites = SystemAPI.GetComponentLookup<ConstructionSite>(true);
            var shown = SystemAPI.GetComponentLookup<LightsOn>();
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (lights, offline, root) in SystemAPI.Query<RefRW<BuildingLights>, EnabledRefRO<Offline>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (!lights.ValueRW.Advance(!offline.ValueRO, deltaTime))
                {
                    continue;
                }

                CollectLit(root, sites, linked, children, parts, site);
                foreach (var part in parts)
                {
                    if (shown.HasComponent(part))
                    {
                        shown[part] = new LightsOn { Value = lights.ValueRO.Value };
                    }
                }
            }
        }

        // The site's meshes are left out, so its work lights stay on while the building is dark.
        private static void CollectLit(Entity root, in ComponentLookup<ConstructionSite> sites,
            in BufferLookup<LinkedEntityGroup> linked, in BufferLookup<Child> children, NativeList<Entity> parts,
            NativeList<Entity> site)
        {
            RenderHierarchy.Collect(root, linked, children, parts);
            if (!sites.TryGetComponent(root, out var dressing) || dressing.Visual == Entity.Null)
            {
                return;
            }

            RenderHierarchy.Collect(dressing.Visual, linked, children, site);
            for (var i = parts.Length - 1; i >= 0; i--)
            {
                if (site.Contains(parts[i]))
                {
                    parts.RemoveAtSwapBack(i);
                }
            }
        }
    }
}
