using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="DamageStage"/> model matching health and bursts the stage's rig on entering it.
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
                var index = IModelStage.Pick(stages.AsNativeArray(), health.ValueRO.Fraction);
                var previous = shown.ValueRO.Index;
                if (index == previous)
                {
                    continue;
                }

                shown.ValueRW.Index = index;
                IModelStage.Show(stages.AsNativeArray(), index, transforms);

                // The first frame only sets the look; a spawned or loaded wreck doesn't replay its damage.
                if (previous >= 0 && index > previous && stages[index].Effect.IsValid())
                {
                    _pool.Play(stages[index].Effect.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }
            }
        }
    }
}
