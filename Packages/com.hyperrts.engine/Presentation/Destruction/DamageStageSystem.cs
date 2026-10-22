using HyperRTS.Core;
using HyperRTS.Presentation.Effects;
using HyperRTS.Presentation.Rendering;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Destruction
{
    /// <summary>
    /// Shows the <see cref="DamageStage"/> model matching health and bursts the stage's rig on entering it.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class DamageStageSystem : SystemBase
    {
        private EffectPool _pool;
        private RenderSwitch _switch;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _switch = new RenderSwitch(ref CheckedStateRef);
            RequireForUpdate<ShownDamageStage>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _switch.Update(ref CheckedStateRef);
            foreach (var (health, stages, shown, place, entity) in SystemAPI
                         .Query<RefRO<Health>, DynamicBuffer<DamageStage>, RefRW<ShownDamageStage>, RefRO<LocalToWorld>>()
                         .WithChangeFilter<Health>().WithEntityAccess())
            {
                var index = IModelStage.Pick(stages.AsNativeArray(), health.ValueRO.Fraction);
                var previous = shown.ValueRO.Index;
                if (index == previous)
                {
                    continue;
                }

                shown.ValueRW.Index = index;
                _switch.ShowStage(entity, stages.AsNativeArray(), index);

                // The first frame only sets the look; a spawned or loaded wreck doesn't replay its damage.
                if (previous >= 0 && index > previous && stages[index].Effect.IsValid())
                {
                    _pool.Play(stages[index].Effect.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }
            }
        }
    }
}
