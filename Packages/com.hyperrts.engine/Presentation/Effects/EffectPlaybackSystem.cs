using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Effects
{
    /// <summary>
    /// Bursts the frame's <see cref="CueEvent"/>s from the pool: the source type's slot rig at the socket, facing the
    /// event's direction, or for impacts the shooter's <see cref="SurfaceEffects"/> rig for the material struck. Skips what the local player can't see and what is beyond a rig's range.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EffectPlaybackSystem : SystemBase
    {
        private TypeCatalog<EffectSet> _catalog;
        private EffectPool _pool;
        private EntityQuery _fog;
        private Camera _camera;
        private Unity.Mathematics.Random _random = new(0x2545F491u);

        protected override void OnCreate()
        {
            _catalog = new TypeCatalog<EffectSet>(SystemAPI.QueryBuilder().WithAll<EntityInfo>()
                .WithAny<EntityEffect, EntityImpactEffects>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                EffectSet.Read);
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _fog = SystemAPI.QueryBuilder().WithAll<FogOfWar>().Build();
            RequireForUpdate<PrefabRegistry>();
            RequireForUpdate<CueQueue>();
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
            var hasTerrain = SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            foreach (var cue in SystemAPI.GetSingletonBuffer<CueEvent>(true))
            {
                if (!CueRules.IsSeenLocally(cue, view, fog, relations))
                {
                    continue;
                }

                if (cue.Slot == CueSlot.Impact)
                {
                    PlayImpact(cue, registry, terrain, hasTerrain);
                }
                else
                {
                    PlayEffect(cue, registry);
                }
            }
        }

        private void PlayEffect(in CueEvent effect, in PrefabRegistry registry)
        {
            var rig = _pool.Get(FindRig(effect, registry));
            if (rig == null || !EffectRange.InRange(_camera, rig, effect.Position))
            {
                return;
            }

            rig.Burst(effect.Position, EffectPose.Facing(effect.Direction), rig.ScaleFor(0f), EffectDetail.Density);
        }

        private GameObject FindRig(in CueEvent effect, in PrefabRegistry registry)
        {
            var set = _catalog.Find(EntityManager, registry, effect.TypeId);
            if (set == null || !set.TryGet(effect.Slot, out var entry))
            {
                return null;
            }

            return entry.Rig.IsValid() ? entry.Rig.Value : null;
        }

        private void PlayImpact(in CueEvent impact, in PrefabRegistry registry, in TerrainHeight terrain,
            bool hasTerrain)
        {
            var surfaces = _catalog.Find(EntityManager, registry, impact.TypeId)?.Impacts;
            if (surfaces == null)
            {
                return;
            }

            var rig = _pool.Get(surfaces.RigFor(impact.Material));
            if (rig == null || !EffectRange.InRange(_camera, rig, impact.Position))
            {
                return;
            }

            var spin = _random.NextFloat(2f * Mathf.PI);
            var (position, rotation) = EffectPose.Impact(impact, terrain, hasTerrain, rig.alignToGround, spin);
            rig.Burst(position, rotation, rig.ScaleFor(impact.Radius), EffectDetail.Density);
        }
    }
}
