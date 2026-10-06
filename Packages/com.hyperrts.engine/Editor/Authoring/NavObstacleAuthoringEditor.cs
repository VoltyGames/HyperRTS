using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Navigation;
using UnityEditor;

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
            GroundHandles.EditBox(obstacle, obstacle.transform.position, obstacle.size, NavColors.Blocked,
                "Resize Nav Obstacle", size => obstacle.size = size);
        }
    }
}
