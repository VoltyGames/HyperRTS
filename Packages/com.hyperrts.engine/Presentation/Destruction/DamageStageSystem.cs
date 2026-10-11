using HyperRTS.Core;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="DamageStage"/> model matching each entity's health and hides the others (scaled to
    /// zero, which leaves fog's rendering toggles alone), playing the stage's effect (a self-destroying particle
    /// prefab) when health drops into it.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class DamageStageSystem : SystemBase
    {
        protected override void OnCreate() => RequireForUpdate<ShownDamageStage>();

        protected override void OnUpdate()
        {
            CompleteDependency();
            var transforms = SystemAPI.GetComponentLookup<LocalTransform>();
            foreach (var (health, stages, shown, place) in SystemAPI
                         .Query<RefRO<Health>, DynamicBuffer<DamageStage>, RefRW<ShownDamageStage>, RefRO<LocalToWorld>>())
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
                    Object.Instantiate(stages[index].Effect.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }
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
