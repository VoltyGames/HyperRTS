using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="DamageStage"/> model matching health (others scaled to zero, leaving fog's toggles alone)
    /// and bursts the stage's rig on entering it.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DamageStageSystem : SystemBase
    {
        private EffectPool _pool;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            RequireForUpdate<ShownDamageStage>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            var transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            foreach (var (health, stages, shown, place) in SystemAPI
                         .Query<RefRO<Health>, DynamicBuffer<DamageStage>, RefRW<ShownDamageStage>, RefRO<LocalToWorld>>()
                         .WithChangeFilter<Health>())
            {
                var index = Current(stages, health.ValueRO.Fraction);
                var previous = shown.ValueRO.Index;
                if (index == previous)
                {
                    continue;
                }

                shown.ValueRW.Index = index;
                for (var i = 0; i < stages.Length; i++)
                {
                    if (transforms.TryGetComponent(stages[i].Visual, out var visual))
                    {
                        visual.Scale = i == index ? 1f : 0f;
                        transforms[stages[i].Visual] = visual;
                    }
                }

                // The first frame only sets the look; a spawned or loaded wreck doesn't replay its damage.
                if (previous >= 0 && index > previous && stages[index].Effect.IsValid())
                {
                    Play(stages[index].Effect.Value, place.ValueRO);
                }
            }
        }

        private void Play(GameObject prefab, in LocalToWorld place)
        {
            var rig = _pool.Get(prefab);
            if (rig != null)
            {
                rig.Pick().Burst(place.Position, place.Rotation, 1f, EffectDetail.Density);
            }
        }

        /// <summary>The most damaged stage whose threshold the health fraction is at or below.</summary>
        private static int Current(DynamicBuffer<DamageStage> stages, float fraction)
        {
            var current = 0;
            for (var i = 0; i < stages.Length; i++)
            {
                if (fraction <= stages[i].Below)
                {
                    current = i;
                }
            }

            return current;
        }
    }
}
