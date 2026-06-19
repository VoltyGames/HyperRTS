using Unity.Entities;
using Unity.Rendering;
using UnityEngine;

namespace HyperRTS.Core.Selection
{
    /// <summary>Bakes a selectable entity: <see cref="Selectable"/>, disabled <see cref="Selected"/>, type, highlight colours and a base-colour override.</summary>
    public class SelectableAuthoring : MonoBehaviour
    {
        public SelectableKind kind = SelectableKind.Unit;
        public Color selectedColor = SelectionComponents.DefaultSelectedColor;
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
                AddComponent(entity, new URPMaterialPropertyBaseColor { Value = deselected });
            }
        }
    }

    /// <summary>Tag marking an entity that can be selected.</summary>
    public struct Selectable : IComponentData { }
}
