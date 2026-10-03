using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>
    /// Placeholder highlight: a parallel job writes each selectable's base colour from its <see cref="Selected"/>
    /// state. The change filter limits it to chunks whose selection changed, so idle frames write nothing and
    /// Entities Graphics re-uploads no material data. (Highlight colours are baked; runtime edits need a re-select.)
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct SelectionHighlightSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            new HighlightJob().ScheduleParallel();
        }

        [BurstCompile]
        [WithPresent(typeof(Selected))]
        // Max two filter types: selection toggled, or the colour override was just added (own writes don't re-trigger).
        [WithChangeFilter(typeof(Selected), typeof(URPMaterialPropertyBaseColor))]
        private partial struct HighlightJob : IJobEntity
        {
            private void Execute(ref URPMaterialPropertyBaseColor color, in SelectionHighlightColors colors,
                EnabledRefRO<Selected> selected)
            {
                color.Value = selected.ValueRO ? colors.Selected : colors.Deselected;
            }
        }
    }
}
