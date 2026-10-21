using HyperRTS.Core;
using HyperRTS.Presentation.Common;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Cues;
using HyperRTS.Simulation.Navigation;
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
        private CueVisibility _visibility;

        protected override void OnCreate()
        {
            _catalog = new TypeCatalog<EffectSet>(SystemAPI.QueryBuilder().WithAll<EntityInfo>()
                .WithAny<EntityEffect, EntityImpactEffects>().WithOptions(EntityQueryOptions.IncludePrefab).Build(),
                EffectSet.Read);
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<PrefabRegistry>();
            RequireForUpdate<CueQueue>();
        }

        protected override void OnUpdate()
        {
            _visibility.Begin();
            var registry = SystemAPI.GetSingleton<PrefabRegistry>();
            var hasTerrain = SystemAPI.TryGetSingleton(out TerrainHeight terrain);
            foreach (var cue in SystemAPI.GetSingletonBuffer<CueEvent>(true))
            {
                if (!_visibility.IsSeen(cue))
                {
                    continue;
                }

                if (cue.Slot == CueSlot.Impact)
                {
                    PlayImpact(cue, registry, terrain, hasTerrain);
                }
                else
                {
                    _pool.Play(FindRig(cue, registry), cue.Position, EffectPose.Facing(cue.Direction));
                }
            }
        }

        private GameObject FindRig(in CueEvent effect, in PrefabRegistry registry)
        {
            var set = _catalog.Find(EntityManager, registry, effect.TypeId);
            if (set == null || !set.TryGet(effect.Slot, effect.Variant, out var entry))
            {
                return null;
            }

            return entry.Rig.IsValid() ? entry.Rig.Value : null;
        }

        private void PlayImpact(in CueEvent impact, in PrefabRegistry registry, in TerrainHeight terrain,
            bool hasTerrain)
        {
            var prefab = _catalog.Find(EntityManager, registry, impact.TypeId)?.Impacts?.RigFor(impact.Material);
            var rig = _pool.Get(prefab);
            if (rig == null || !_pool.InRange(rig, impact.Position))
            {
                return;
            }

            // Variants share the root's ground alignment, since the pose is set before one is picked.
            var (position, rotation) = EffectPose.Impact(impact, terrain, hasTerrain, rig.alignToGround);
            rig.Play(position, rotation, impact.Radius);
        }
    }
}
