using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>
    /// Adds the simulation selection component set to a runtime-created entity, mirroring <see cref="SelectableAuthoring"/>.
    /// Single source of truth for the default highlight colours and the sRGB-&gt;linear conversion both paths use.
    /// The render-side base-colour override is added separately by the presentation layer
    /// (<c>SelectionHighlightColorInitSystem</c>), keeping this sim-only.
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

        public static void AddTo(EntityCommandBuffer ecb, Entity entity, SelectableKind kind)
        {
            AddTo(ecb, entity, kind, ToLinear(DefaultSelectedColor), ToLinear(DefaultDeselectedColor));
        }

        public static void AddTo(EntityCommandBuffer ecb, Entity entity, SelectableKind kind,
            float4 selectedColor, float4 deselectedColor)
        {
            ecb.AddComponent<Selectable>(entity);
            ecb.AddComponent<Selected>(entity);
            ecb.SetComponentEnabled<Selected>(entity, false);
            ecb.AddComponent(entity, new SelectableType { Kind = kind });
            ecb.AddComponent(entity, new SelectionHighlightColors { Selected = selectedColor, Deselected = deselectedColor });
        }

        /// <summary>Immediate variant of <see cref="AddTo(EntityCommandBuffer, Entity, SelectableKind)"/>.</summary>
        public static void AddTo(EntityManager entityManager, Entity entity, SelectableKind kind)
        {
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            AddTo(ecb, entity, kind);
            ecb.Playback(entityManager);
            ecb.Dispose();
        }
    }
}
