using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draggable blocked area for nav obstacles.</summary>
    [CustomEditor(typeof(NavObstacleAuthoring))]
    [CanEditMultipleObjects]
    public class NavObstacleAuthoringEditor : AuthoringEditor
    {
        private void OnSceneGUI()
        {
            var obstacle = (NavObstacleAuthoring)target;

            EditorGUI.BeginChangeCheck();
            var size = GroundHandles.Box(obstacle.transform.position, obstacle.size, Color.red);
            if (EditorGUI.EndChangeCheck())
            {
                QuickFixes.Edit(obstacle, "Resize Nav Obstacle", () => obstacle.size = size);
            }
        }
    }
}
