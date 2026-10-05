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
            var size = RTSHandles.Footprint(obstacle.transform.position, obstacle.size, Color.red);
            if (Changed("Resize Nav Obstacle"))
            {
                obstacle.size = size;
            }
        }
    }
}
