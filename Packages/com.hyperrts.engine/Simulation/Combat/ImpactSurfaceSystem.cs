using HyperRTS.Core;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// Sets each impact cue's <see cref="ImpactSurface"/> and <see cref="SurfaceMaterial"/> where the targets live, so
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

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _buildings = state.GetComponentLookup<BuildingTag>(true);
            _agents = state.GetComponentLookup<NavAgent>(true);
            _materials = state.GetComponentLookup<HitMaterial>(true);
            state.RequireForUpdate<CueQueue>();
            state.RequireForUpdate<NavGrid>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _buildings.Update(ref state);
            _agents.Update(ref state);
            _materials.Update(ref state);
            var hasGround = SystemAPI.TryGetSingleton<GroundMaterials>(out var ground);
            new ClassifyJob
            {
                Buildings = _buildings,
                Agents = _agents,
                Materials = _materials,
                Grid = SystemAPI.GetSingleton<NavGrid>(),
                Ground = ground,
                HasGround = hasGround,
            }.Schedule();
        }

        [BurstCompile]
        [WithAll(typeof(CueQueue))]
        private partial struct ClassifyJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<BuildingTag> Buildings;
            [ReadOnly] public ComponentLookup<NavAgent> Agents;
            [ReadOnly] public ComponentLookup<HitMaterial> Materials;
            [ReadOnly] public NavGrid Grid;
            public GroundMaterials Ground;
            public bool HasGround;

            private void Execute(DynamicBuffer<CueEvent> cues)
            {
                for (var i = 0; i < cues.Length; i++)
                {
                    var impact = cues[i];
                    if (impact.Slot != CueSlot.Impact)
                    {
                        continue;
                    }

                    impact.Surface = Classify(impact);
                    impact.Material = ResolveMaterial(impact);
                    cues[i] = impact;
                }
            }

            private ImpactSurface Classify(in CueEvent impact)
            {
                if (Buildings.HasComponent(impact.Target))
                {
                    return ImpactSurface.Structure;
                }

                if (NavAgent.GetLayer(Agents, impact.Target) == NavLayer.Air)
                {
                    return ImpactSurface.Air;
                }

                return SurfaceAt(impact.Position);
            }

            /// <summary>The target's own material wins; open ground comes from the baked ground material map.</summary>
            private SurfaceMaterial ResolveMaterial(in CueEvent impact)
            {
                if (Materials.TryGetComponent(impact.Target, out var hit))
                {
                    return hit.Value;
                }

                switch (impact.Surface)
                {
                    case ImpactSurface.Water:
                        return SurfaceMaterial.Water;
                    case ImpactSurface.Ground when HasGround:
                        return Ground.At(impact.Position.xz);
                    default:
                        return SurfaceMaterial.None;
                }
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
