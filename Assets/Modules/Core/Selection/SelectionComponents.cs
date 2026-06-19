using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace HyperRTS.Core.Selection
{
    /// <summary>
    /// Adds the selection component set to a runtime-created entity, mirroring <see cref="SelectableAuthoring"/>.
    /// Single source of truth for the default highlight colours and the sRGB-&gt;linear conversion both paths use.
    /// </summary>
    public static class SelectionComponents
    {
        public static readonly Color DefaultSelectedColor = new(0.2f, 1f, 0.35f, 1f);
        public static readonly Color DefaultDeselectedColor = Color.white;

        /// <summary>Converts an sRGB <see cref="Color"/> to the linear-RGBA <see cref="float4"/> stored in components.</summary>
        public static float4 ToLinear(Color color)
        {
            var linear = color.linear;
            return new float4(linear.r, linear.g, linear.b, linear.a);
        }

        public static void AddTo(EntityManager entityManager, Entity entity, SelectableKind kind)
        {
            AddTo(entityManager, entity, kind, ToLinear(DefaultSelectedColor), ToLinear(DefaultDeselectedColor));
        }

        public static void AddTo(EntityManager entityManager, Entity entity, SelectableKind kind,
            float4 selectedColor, float4 deselectedColor)
        {
            entityManager.AddComponent<Selectable>(entity);
            entityManager.AddComponent<Selected>(entity);
            entityManager.SetComponentEnabled<Selected>(entity, false);
            entityManager.AddComponentData(entity, new SelectableType { Kind = kind });
            entityManager.AddComponentData(entity,
                new SelectionHighlightColors { Selected = selectedColor, Deselected = deselectedColor });
            entityManager.AddComponentData(entity, new URPMaterialPropertyBaseColor { Value = deselectedColor });
        }
    }
}
