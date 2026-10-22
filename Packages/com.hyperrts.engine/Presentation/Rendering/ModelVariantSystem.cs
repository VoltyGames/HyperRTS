using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Shows each new entity's picked <see cref="ModelVariants"/> model and hides the others.</summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial class ModelVariantSystem : SystemBase
    {
        private RenderSwitch _switch;

        protected override void OnCreate()
        {
            _switch = new RenderSwitch(ref CheckedStateRef);
            RequireForUpdate<ModelVariants>();
        }

        protected override void OnUpdate()
        {
            CompleteDependency();
            _switch.Update(ref CheckedStateRef);
            var all = new NativeList<Entity>(Allocator.Temp);
            foreach (var (variants, parts, transform, entity) in SystemAPI
                         .Query<RefRW<ModelVariants>, DynamicBuffer<ModelVariantPart>, RefRO<LocalTransform>>()
                         .WithEntityAccess())
            {
                if (variants.ValueRO.Shown >= 0)
                {
                    continue;
                }

                var index = variants.ValueRO.Pick(transform.ValueRO.Position);
                variants.ValueRW.Shown = index;
                all.Clear();
                _switch.Collect(entity, all);
                foreach (var part in parts)
                {
                    _switch.SetBranch(all, part.Visual, HideReason.Variant, part.Variant != index);
                }
            }
        }
    }
}
