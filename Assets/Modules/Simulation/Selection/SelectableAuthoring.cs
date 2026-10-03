using HyperRTS.Core;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Bakes the selection components; the presentation layer adds the colour override at runtime.</summary>
    [AddComponentMenu(HyperRTSMenu.Selection + "Selectable")]
    [Icon(HyperRTSIcons.Selection)]
    [HelpURL(HyperRTSDocs.WorldSetup)]
    [DisallowMultipleComponent]
    public class SelectableAuthoring : MonoBehaviour
    {
        [Tooltip("Whether this entity is a unit or a building.")]
        public SelectableKind kind = SelectableKind.Unit;

        [Header("Highlight")]
        [Tooltip("Tint applied while selected.")]
        public Color selectedColor = SelectionComponents.DefaultSelectedColor;

        [Tooltip("Base tint while not selected.")]
        public Color baseColor = SelectionComponents.DefaultDeselectedColor;

        public class Baker : Baker<SelectableAuthoring>
        {
            public override void Bake(SelectableAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                var selected = SelectionComponents.ToLinear(authoring.selectedColor);
                var deselected = SelectionComponents.ToLinear(authoring.baseColor);

                AddComponent<Selectable>(entity);
                AddComponent<Selected>(entity);
                SetComponentEnabled<Selected>(entity, false);
                AddComponent(entity, new SelectableType { Kind = authoring.kind });
                AddComponent(entity, new SelectionHighlightColors { Selected = selected, Deselected = deselected });
            }
        }
    }

    public struct Selectable : IComponentData { }
}
