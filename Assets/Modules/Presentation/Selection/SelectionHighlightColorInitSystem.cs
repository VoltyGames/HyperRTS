using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>
    /// Presentation-only: gives every selectable a per-entity base-colour override so the highlight can
    /// drive it. Adds <see cref="URPMaterialPropertyBaseColor"/> to anything with
    /// <see cref="SelectionHighlightColors"/> that lacks it - baked and factory-spawned alike - in one
    /// chunk-level batch; <see cref="SelectionHighlightSystem"/> then seeds the colour (the new component
    /// passes its change filter). A headless server has no presentation world, so it never adds the render component.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    [UpdateBefore(typeof(SelectionHighlightSystem))]
    public partial struct SelectionHighlightColorInitSystem : ISystem
    {
        private EntityQuery _missingColor;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            _missingColor = SystemAPI.QueryBuilder()
                .WithAll<SelectionHighlightColors>()
                .WithNone<URPMaterialPropertyBaseColor>()
                .Build();
            state.RequireForUpdate(_missingColor);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            state.EntityManager.AddComponent<URPMaterialPropertyBaseColor>(_missingColor);
        }
    }
}
