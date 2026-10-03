using HyperRTS.Simulation.Selection;
using Unity.Burst;
using Unity.Entities;
using Unity.Rendering;

namespace HyperRTS.Presentation.Selection
{
    /// <summary>Adds the base-colour override to selectables that lack it. Presentation-only, so headless servers skip it.</summary>
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
