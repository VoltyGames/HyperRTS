using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>
    /// Presentation-only: gives every selectable a per-entity base-colour override so the
    /// highlight can drive it. Adds <see cref="URPMaterialPropertyBaseColor"/> (seeded to the
    /// deselected colour) to anything with <see cref="SelectionHighlightColors"/> that lacks it -
    /// covering both baked and factory-spawned entities. A headless server has no presentation
    /// world, so it simply never adds the render component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SelectionHighlightSystem))]
    public partial struct SelectionHighlightColorInitSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (colors, entity) in
                     SystemAPI.Query<RefRO<SelectionHighlightColors>>()
                         .WithNone<URPMaterialPropertyBaseColor>()
                         .WithEntityAccess())
            {
                ecb.AddComponent(entity, new URPMaterialPropertyBaseColor { Value = colors.ValueRO.Deselected });
            }

            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }
    }
}
