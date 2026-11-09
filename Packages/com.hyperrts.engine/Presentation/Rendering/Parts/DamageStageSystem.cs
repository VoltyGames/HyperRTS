using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Destruction;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.Parts
{
    /// <summary>
    /// Shows the <see cref="DamageStage"/> model matching health and bursts the stage's rig on entering it. The first
    /// stage hides in place; later ones are copied in from their prefabs while they show.
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
            var copies = ModelCopies.Create();
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
                ShowModel(entity, stages, index, ref copies);

                // The first frame only sets the look; a spawned or loaded wreck doesn't replay its damage.
                if (previous >= 0 && index > previous && stages[index].Effect.IsValid())
                {
                    _pool.Play(stages[index].Effect.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }
            }

            copies.Apply(EntityManager);
        }

        // Stages without a model leave the shown one alone.
        private void ShowModel(Entity entity, DynamicBuffer<DamageStage> stages, int index, ref ModelCopies copies)
        {
            if (stages[0].Visual != Entity.Null)
            {
                _switch.SetBranch(entity, stages[0].Visual, HideReason.Stage, index != 0);
            }

            for (var i = 1; i < stages.Length; i++)
            {
                if (stages[i].Visual != Entity.Null)
                {
                    copies.Set(entity, stages[i].Visual, i == index);
                }
            }
        }
    }
}
