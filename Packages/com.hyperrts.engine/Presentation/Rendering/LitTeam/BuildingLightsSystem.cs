using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Buildings.Construction;
using HyperRTS.Simulation.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Rendering.LitTeam
{
    /// <summary>
    /// Writes each building's <see cref="BuildingLights"/> to <see cref="LightsOn"/> on its meshes: dark while
    /// <see cref="Offline"/>, lit otherwise, with a flicker on each change.
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct BuildingLightsSystem : ISystem
    {
        private EntityQuery _unseen;
        private MeshPainter<LightsOn> _painter;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _unseen = SystemAPI.QueryBuilder().WithAll<BuildingTag>().WithPresent<Offline>().WithNone<BuildingLights>()
                .Build();
            _painter = new MeshPainter<LightsOn>(ref state);
            state.RequireForUpdate(SystemAPI.QueryBuilder().WithAll<BuildingTag>().WithPresent<Offline>().Build());
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _painter.Update(ref state);
            var parts = new NativeList<Entity>(Allocator.Temp);
            var site = new NativeList<Entity>(Allocator.Temp);
            var commands = new EntityCommandBuffer(Allocator.Temp);

            foreach (var root in _unseen.ToEntityArray(Allocator.Temp))
            {
                var lights = BuildingLights.Seen(!SystemAPI.IsComponentEnabled<Offline>(root));
                CollectLit(ref state, root, parts, site);
                _painter.Add(parts, new LightsOn { Value = lights.Value }, commands);
                commands.AddComponent(root, lights);
            }

            commands.Playback(state.EntityManager);
            // After playback, so the painter sees the meshes that just got the property.
            _painter.Update(ref state);
            var deltaTime = SystemAPI.Time.DeltaTime;
            foreach (var (lights, offline, root) in SystemAPI.Query<RefRW<BuildingLights>, EnabledRefRO<Offline>>()
                         .WithOptions(EntityQueryOptions.IgnoreComponentEnabledState).WithEntityAccess())
            {
                if (lights.ValueRW.Advance(!offline.ValueRO, deltaTime))
                {
                    CollectLit(ref state, root, parts, site);
                    _painter.Set(parts, new LightsOn { Value = lights.ValueRO.Value });
                }
            }
        }

        // The site's meshes are left out, so its work lights stay on while the building is dark.
        private void CollectLit(ref SystemState state, Entity root, NativeList<Entity> parts, NativeList<Entity> site)
        {
            _painter.Collect(root, parts);
            var sites = SystemAPI.GetComponentLookup<ConstructionSite>(true);
            if (!sites.TryGetComponent(root, out var dressing) || dressing.Visual == Entity.Null)
            {
                return;
            }

            _painter.Collect(dressing.Visual, site);
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
