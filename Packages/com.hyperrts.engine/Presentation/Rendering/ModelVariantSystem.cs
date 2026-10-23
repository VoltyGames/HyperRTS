using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>Shows each new entity's picked <see cref="ModelVariants"/> model and hides the others.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(EntitiesGraphicsSystem))]
    public partial struct ModelVariantSystem : ISystem
    {
        private RenderSwitch _switch;
        private EntityQuery _unpicked;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _switch = new RenderSwitch(ref state);
            _unpicked = SystemAPI.QueryBuilder().WithAll<ModelVariants>().WithDisabled<ModelVariantPicked>().Build();
            state.RequireForUpdate(_unpicked);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.CompleteDependency();
            _switch.Update(ref state);
            var all = new NativeList<Entity>(Allocator.Temp);
            foreach (var (variants, picked, parts, transform, entity) in SystemAPI
                         .Query<RefRW<ModelVariants>, EnabledRefRW<ModelVariantPicked>, DynamicBuffer<ModelVariantPart>,
                             RefRO<LocalTransform>>().WithDisabled<ModelVariantPicked>().WithEntityAccess())
            {
                var index = variants.ValueRO.Pick(transform.ValueRO.Position);
                variants.ValueRW.Shown = index;
                picked.ValueRW = true;
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
