using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Faint ground footprints for objects that aren't selected; the selected one shows its drag handles.</summary>
    public static class FootprintGizmos
    {
        private const float Alpha = 0.25f;

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        private static void DrawNavArea(NavAreaAuthoring area, GizmoType type) =>
            Draw(area.transform.position, area.size, NavColors.Get(area.kind));

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        private static void DrawNavObstacle(NavObstacleAuthoring obstacle, GizmoType type) =>
            Draw(obstacle.transform.position, obstacle.size, NavColors.Blocked);

        [DrawGizmo(GizmoType.NotInSelectionHierarchy)]
        private static void DrawBuilding(BuildingAuthoring building, GizmoType type) =>
            Draw(building.transform.position, building.footprint, SceneMatch.PlayerColor(building.owner));

        private static void Draw(Vector3 center, Vector2 size, Color color)
        {
            Gizmos.color = GroundHandles.Faded(color, Alpha);
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0f, size.y));
        }
    }
}
