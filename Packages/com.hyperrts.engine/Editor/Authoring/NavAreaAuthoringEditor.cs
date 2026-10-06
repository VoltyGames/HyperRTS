using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Navigation;
using UnityEditor;

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
            GroundHandles.EditBox(area, area.transform.position, area.size, NavColors.Of(area.kind), "Resize Nav Area",
                size => area.size = size);
        }
    }
}
