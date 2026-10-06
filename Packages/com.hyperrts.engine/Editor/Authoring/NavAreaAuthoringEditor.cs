using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draggable box for nav areas, coloured by kind.</summary>
    [CustomEditor(typeof(NavAreaAuthoring))]
    [CanEditMultipleObjects]
    public class NavAreaAuthoringEditor : AuthoringEditor
    {
        private void OnSceneGUI()
        {
            var area = (NavAreaAuthoring)target;

            EditorGUI.BeginChangeCheck();
            var size = GroundHandles.Box(area.transform.position, area.size, ColorOf(area.kind));
            if (EditorGUI.EndChangeCheck())
            {
                QuickFixes.Edit(area, "Resize Nav Area", () => area.size = size);
            }
        }

        private static Color ColorOf(NavAreaKind kind)
        {
            switch (kind)
            {
                case NavAreaKind.Water:
                    return new Color(0.2f, 0.45f, 1f);
                case NavAreaKind.Blocked:
                    return Color.red;
                default:
                    return new Color(1f, 0.8f, 0.2f);
            }
        }
    }
}
