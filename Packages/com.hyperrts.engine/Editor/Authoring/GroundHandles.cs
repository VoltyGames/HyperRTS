using System;
using HyperRTS.Editor.Common;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

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

        /// <summary><see cref="Radius"/> that applies a drag to <paramref name="owner"/> as one undo step.</summary>
        public static void EditRadius(Object owner, Vector3 center, float radius, Color color, string label,
            string undoLabel, Action<float> apply)
        {
            EditorGUI.BeginChangeCheck();
            var edited = Radius(center, radius, color, label);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUndo.Record(owner, undoLabel, () => apply(edited));
            }
        }

        /// <summary>Centred X by Z box; dragging an edge resizes both sides so the footprint stays centred.</summary>
        public static Vector2 Box(Vector3 center, Vector2 size, Color color)
        {
            BoxHandle.center = center;
            BoxHandle.size = new Vector3(size.x, 0f, size.y);
            BoxHandle.SetColor(color);
            BoxHandle.DrawHandle();

            var handleCenter = BoxHandle.center - center;
            var handleSize = BoxHandle.size;
            return new Vector2(MirroredSize(size.x, handleCenter.x, handleSize.x),
                MirroredSize(size.y, handleCenter.z, handleSize.z));
        }

        /// <summary><see cref="Box"/> that applies a drag to <paramref name="owner"/> as one undo step.</summary>
        public static void EditBox(Object owner, Vector3 center, Vector2 size, Color color, string undoLabel,
            Action<Vector2> apply)
        {
            EditorGUI.BeginChangeCheck();
            var edited = Box(center, size, color);
            if (EditorGUI.EndChangeCheck())
            {
                EditorUndo.Record(owner, undoLabel, () => apply(edited));
            }
        }

        /// <summary>
        /// Size along one axis of a box centred on 0 after the handle moved one of its faces (keeping the other), with
        /// the moved face mirrored onto the other side so the box stays centred.
        /// </summary>
        public static float MirroredSize(float size, float handleCenter, float handleSize)
        {
            var max = handleCenter + handleSize * 0.5f;
            var min = handleCenter - handleSize * 0.5f;
            var maxShift = Mathf.Abs(max - size * 0.5f);
            var minShift = Mathf.Abs(min + size * 0.5f);
            var moved = maxShift >= minShift ? max : min;
            return 2f * Mathf.Abs(moved);
        }

        public static Color Faded(Color color, float alpha = 0.35f) => new(color.r, color.g, color.b, alpha);
    }
}
