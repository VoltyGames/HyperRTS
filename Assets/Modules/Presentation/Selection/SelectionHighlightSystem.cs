using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>Placeholder highlight: writes each selectable's base colour from its <see cref="Selected"/> state.</summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SelectionHighlightSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (color, colors, entity) in
                     SystemAPI.Query<RefRW<URPMaterialPropertyBaseColor>, RefRO<SelectionHighlightColors>>()
                         .WithEntityAccess())
            {
                var selected = SystemAPI.IsComponentEnabled<Selected>(entity);
                var target = selected ? colors.ValueRO.Selected : colors.ValueRO.Deselected;

                // Only touch ValueRW when the colour changes, so unchanged chunks aren't re-uploaded each frame.
                if (math.any(color.ValueRO.Value != target))
                {
                    color.ValueRW.Value = target;
                }
            }
        }
    }
}
