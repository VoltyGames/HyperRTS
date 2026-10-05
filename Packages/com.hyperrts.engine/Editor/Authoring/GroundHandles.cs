using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Scene-view handles that lie flat on the ground and drag along X and Z.</summary>
    public static class GroundHandles
    {
        private static readonly BoxBoundsHandle BoxHandle = new()
        {
            axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Z,
        };

        /// <summary>Ground disc with a drag dot on its +X edge; returns the edited radius.</summary>
        public static float Radius(Vector3 center, float radius, Color color, string label)
        {
            Handles.color = color;
            Handles.DrawWireDisc(center, Vector3.up, radius);
            var dot = center + Vector3.right * radius;
            Handles.Label(dot + Vector3.forward * 0.3f, $"{label} {radius:0.##}");
            var moved = Handles.Slider(dot, Vector3.right, HandleUtility.GetHandleSize(dot) * 0.08f, Handles.DotHandleCap, 0f);
            return Mathf.Max(0f, moved.x - center.x);
        }

        /// <summary>Centred X by Z box; dragging an edge resizes both sides so the footprint stays centred.</summary>
        public static Vector2 Box(Vector3 center, Vector2 size, Color color)
        {
            BoxHandle.center = center;
            BoxHandle.size = new Vector3(size.x, 0f, size.y);
            BoxHandle.SetColor(color);
            BoxHandle.DrawHandle();

            var shift = BoxHandle.center - center;
            return new Vector2(BoxHandle.size.x + 2f * Mathf.Abs(shift.x), BoxHandle.size.z + 2f * Mathf.Abs(shift.z));
        }

        public static Color Faded(Color color, float alpha = 0.35f) => new(color.r, color.g, color.b, alpha);
    }
}
