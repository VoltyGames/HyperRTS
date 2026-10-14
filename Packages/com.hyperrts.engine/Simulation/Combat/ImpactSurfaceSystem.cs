using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Sets each impact's <see cref="ImpactSurface"/> and <see cref="SurfaceMaterial"/> where the targets live, so
    /// clients and replays get them with the event.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(CombatSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct ImpactSurfaceSystem : ISystem
    {
        private ComponentLookup<BuildingTag> _buildings;
        private ComponentLookup<NavAgent> _agents;
        private ComponentLookup<HitMaterial> _materials;
        private NavGrid _noGrid;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _buildings = state.GetComponentLookup<BuildingTag>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _materials = state.GetComponentLookup<HitMaterial>(true);
            _noGrid = NavGrid.Placeholder();
            state.RequireForUpdate<ImpactQueue>();
        }

        public void OnDestroy(ref SystemState state) => _noGrid.Dispose();

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _buildings.Update(ref state);
            _agents.Update(ref state);
            _materials.Update(ref state);
            var hasGrid = SystemAPI.TryGetSingleton<NavGrid>(out var grid) && grid.IsCreated;
            new ClassifyJob
            {
                Buildings = _buildings,
                Agents = _agents,
                Materials = _materials,
                Grid = hasGrid ? grid : _noGrid,
                HasGrid = hasGrid,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(ImpactQueue))]
        private partial struct ClassifyJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<BuildingTag> Buildings;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<HitMaterial> Materials;
            [ReadOnly] public NavGrid Grid;
            public bool HasGrid;

            private void Execute(DynamicBuffer<ImpactEvent> impacts)
            {
                for (var i = 0; i < impacts.Length; i++)
                {
                    var impact = impacts[i];
                    impact.Surface = Classify(impact);
                    impact.Material = ResolveMaterial(impact);
                    impacts[i] = impact;
                }
            }

            private ImpactSurface Classify(in ImpactEvent impact)
            {
                if (Buildings.HasComponent(impact.Target))
                {
                    return ImpactSurface.Structure;
                }

                if (NavAgent.GetLayer(Agents, impact.Target) == NavLayer.Air)
                {
                    return ImpactSurface.Air;
                }

                return HasGrid ? SurfaceAt(impact.Position) : ImpactSurface.Ground;
            }

            /// <summary>Open ground stays None: presentation reads it from the ground material map.</summary>
            private SurfaceMaterial ResolveMaterial(in ImpactEvent impact)
            {
                if (Materials.TryGetComponent(impact.Target, out var hit))
                {
                    return hit.Value;
                }

                return impact.Surface == ImpactSurface.Water ? SurfaceMaterial.Water : SurfaceMaterial.None;
            }

            /// <summary>A ford's deck lies under the water; a bridge's stands above it.</summary>
            private ImpactSurface SurfaceAt(float3 position)
            {
                var cell = Grid.WorldToCell(position);
                var surface = Grid.Surface(cell);
                if ((surface & NavSurface.Deck) != 0)
                {
                    var bridge = Grid.DeckHeights[Grid.Index(cell)] > Grid.WaterLevel;
                    return bridge ? ImpactSurface.Structure : ImpactSurface.Water;
                }

                return (surface & NavSurface.Water) != 0 ? ImpactSurface.Water : ImpactSurface.Ground;
            }
        }
    }
}
