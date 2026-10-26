using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Selection;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draggable pick box in the Scene view, and a faint outline while the object isn't selected.</summary>
    [CustomEditor(typeof(PickBoxAuthoring))]
    [CanEditMultipleObjects]
    public class PickBoxAuthoringEditor : AuthoringEditor
    {
        private static readonly Color BoxColor = new(0.30f, 0.70f, 1.00f);

        [NoAutoStaticsCleanup]
        private static readonly BoxBoundsHandle Handle = new();

        [DrawGizmo(GizmoType.NonSelected)]
        private static void DrawOutline(PickBoxAuthoring box, GizmoType type)
        {
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.color = GroundHandles.Faded(BoxColor);
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = Matrix4x4.identity;
        }

        private void OnSceneGUI()
        {
            var box = (PickBoxAuthoring)target;
            using var space = new Handles.DrawingScope(BoxColor, box.transform.localToWorldMatrix);
            Handle.center = box.center;
            Handle.size = box.size;
            EditorGUI.BeginChangeCheck();
            Handle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUndo.Record(box, "Resize Pick Box", () =>
                {
                    box.center = Handle.center;
                    box.size = Handle.size;
                });
            }
        }
    }
}
