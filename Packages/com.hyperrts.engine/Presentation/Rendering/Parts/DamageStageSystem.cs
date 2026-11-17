using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Effects.Rig;
using HyperRTS.Presentation.Rendering.Parts;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Combat;
using HyperRTS.Simulation.Common.Systems;
using HyperRTS.Simulation.Destruction;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering.Parts
{
    /// <summary>
    /// Shows the <see cref="DamageStage"/> model matching health and bursts the stage's rig on entering it. The first
    /// stage hides in place; later ones are copied in from their prefabs while they show. A stage entered out of the
    /// local player's sight switches its model without the rig.
    /// </summary>
    [WorldSystemFilter(SimulationWorlds.Presented)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class DamageStageSystem : SystemBase
    {
        private EffectPool _pool;
        private RenderSwitch _switch;
        private CueVisibility _visibility;

        protected override void OnCreate()
        {
            _pool = World.GetOrCreateSystemManaged<EffectPoolSystem>().Pool;
            _switch = new RenderSwitch(ref CheckedStateRef);
            _visibility = new CueVisibility(ref CheckedStateRef);
            RequireForUpdate<ShownDamageStage>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _switch.Update(ref CheckedStateRef);
            _visibility.Begin();
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
                Show(entity, stages, index, ref copies);

                // The first frame only sets the look; a spawned or loaded wreck doesn't replay its damage.
                if (previous >= 0 && index > previous && stages[index].Effect.IsValid()
                    && _visibility.IsSeen(place.ValueRO.Position))
                {
                    _pool.Play(stages[index].Effect.Value, place.ValueRO.Position, place.ValueRO.Rotation);
                }
            }

            copies.Apply(EntityManager);
        }

        private void Show(Entity entity, DynamicBuffer<DamageStage> stages, int index, ref ModelCopies copies)
        {
            var first = stages[0].Visual;
            var model = ShownModel(stages, index);
            _switch.SetBranch(entity, first, HideReason.Stage, model != first);
            for (var i = 1; i < stages.Length; i++)
            {
                // Only a later stage's own model was baked as a prefab to copy.
                var visual = stages[i].Visual;
                if (visual != first && visual != entity)
                {
                    copies.Set(entity, visual, visual == model);
                }
            }
        }

        // A stage without a model keeps the one of the nearest stage above it.
        private static Entity ShownModel(DynamicBuffer<DamageStage> stages, int index)
        {
            while (index > 0 && stages[index].Visual == Entity.Null)
            {
                index--;
            }

            return stages[index].Visual;
        }
    }
}
