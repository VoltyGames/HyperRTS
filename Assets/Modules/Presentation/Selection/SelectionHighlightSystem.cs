using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>Writes each selectable's base colour from <see cref="Selected"/>, only in chunks whose selection changed.</summary>
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
        // Two types max; the colour type catches newly added overrides.
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
