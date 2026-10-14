using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Effects;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Bursts the frame's <see cref="EffectEvent"/>s (the source type's slot rig at the socket, facing the event's
    /// direction) and <see cref="ImpactEvent"/>s (the shooter's <see cref="SurfaceEffects"/> rig for the material
    /// struck, on the ground) from the pool. Skips what the local player can't see and what is beyond a rig's range.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EffectPlaybackSystem : SystemBase
    {
        private EffectCatalog _catalog;
        private EffectPool _pool;
        private EntityQuery _fog;
        private Camera _camera;
        private Unity.Mathematics.Random _random = new(0x2545F491u);

        protected override void OnCreate()
        {
            _catalog = new EffectCatalog(SystemAPI.QueryBuilder().WithAll<EntityInfo>()
                .WithAny<EntityEffect, EntityImpactEffects>().WithOptions(EntityQueryOptions.IncludePrefab).Build());
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            RequireForUpdate<PrefabRegistry>();
        }

        protected override void OnUpdate()
        {
            _camera = EffectRange.Main(_camera);
            if (_camera == null)
            {
                return;
            }

            SystemAPI.TryGetSingleton(out LocalFogView view);
            SystemAPI.TryGetSingleton(out FactionRelations relations);
            _fog.CompleteDependency();
            _fog.TryGetSingleton(out FogOfWar fog);
            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            if (SystemAPI.TryGetSingletonBuffer<EffectEvent>(out var effects, true))
            {
                PlayEffects(effects, view, fog, relations, registry);
            }

            if (SystemAPI.TryGetSingletonBuffer<ImpactEvent>(out var impacts, true))
            {
                PlayImpacts(impacts, view, fog, relations, registry);
            }
        }

        private void PlayEffects(DynamicBuffer<EffectEvent> events, in LocalFogView view, in FogOfWar fog,
            in FactionRelations relations, in PrefabRegistry registry)
        {
            foreach (var effect in events)
            {
                if (!EffectRules.IsSeenLocally(effect, view, fog, relations))
                {
                    continue;
                }

                var prefab = RigOf(effect, registry);
                var rig = prefab != null ? _pool.Get(prefab) : null;
                if (rig == null || !EffectRange.InRange(_camera, rig, effect.Position))
                {
                    continue;
                }

                rig.Burst(effect.Position, EffectPose.Facing(effect.Direction), rig.ScaleFor(0f),
                    EffectPoolSystem.Density);
            }
        }

        private GameObject RigOf(in EffectEvent effect, in PrefabRegistry registry)
        {
            var set = _catalog.Find(EntityManager, registry, effect.TypeId);
            if (set == null || !set.TryGet(effect.Slot, out var entry))
            {
                return null;
            }

            return entry.Rig.IsValid() ? entry.Rig.Value : null;
        }

        private void PlayImpacts(DynamicBuffer<ImpactEvent> events, in LocalFogView view, in FogOfWar fog,
            in FactionRelations relations, in PrefabRegistry registry)
        {
            var hasGround = SystemAPI.TryGetSingleton(out GroundMaterials ground);
            var hasTerrain = SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            foreach (var impact in events)
            {
                var surfaces = _catalog.Find(EntityManager, registry, impact.TypeId)?.Impacts;
                if (surfaces == null || !ImpactRules.IsSeenLocally(impact, view, fog, relations))
                {
                    continue;
                }

                var material = impact.Material;
                if (material == SurfaceMaterial.None && hasGround)
                {
                    material = ground.At(impact.Position.xz);
                }

                var prefab = surfaces.RigFor(material);
                var rig = prefab != null ? _pool.Get(prefab) : null;
                if (rig == null || !EffectRange.InRange(_camera, rig, impact.Position))
                {
                    continue;
                }

                var spin = _random.NextFloat(2f * Mathf.PI);
                var (position, rotation) = EffectPose.Impact(impact, terrain, hasTerrain, rig.alignToGround, spin);
                rig.Burst(position, rotation, rig.ScaleFor(impact.Radius), EffectPoolSystem.Density);
            }
        }
    }
}
