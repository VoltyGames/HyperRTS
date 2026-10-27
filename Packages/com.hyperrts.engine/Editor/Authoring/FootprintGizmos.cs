using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Navigation;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>
    /// Faint ground footprints for objects that aren't selected, the selected one showing its drag handles, and dock
    /// bays always: the stop's reach and facing, the run-in in green and the exit in yellow.
    /// </summary>
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

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        private static void DrawDock(DockAuthoring dock, GizmoType type)
        {
            if (!dock.IsComplete)
            {
                return;
            }

            var root = dock.transform;
            foreach (var bay in dock.Bays())
            {
                var stop = root.TransformPoint(bay.Stop);
                var facing = root.TransformDirection(new Vector3(bay.Facing.x, 0f, bay.Facing.y)).normalized;
                var side = Vector3.Cross(Vector3.up, facing);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(stop - facing * bay.RunIn, stop);
                var tip = stop + facing * BayPose.StopReach * 2f;
                Gizmos.DrawLine(stop, tip);
                Gizmos.DrawLine(tip, tip - (facing - side) * BayPose.StopReach);
                Gizmos.DrawLine(tip, tip - (facing + side) * BayPose.StopReach);
                Handles.color = Color.green;
                Handles.DrawWireDisc(stop, Vector3.up, BayPose.StopReach);
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(stop, root.TransformPoint(bay.Exit));
            }
        }

        private static void Draw(Vector3 center, Vector2 size, Color color)
        {
            Gizmos.color = GroundHandles.Faded(color, Alpha);
            Gizmos.DrawWireCube(center, new Vector3(size.x, 0f, size.y));
        }
    }
}
