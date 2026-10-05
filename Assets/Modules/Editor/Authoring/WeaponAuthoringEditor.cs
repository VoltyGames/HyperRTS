using HyperRTS.Simulation.Combat;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Draws the weapon's real reach (range plus the owner's radius) and lets you drag it.</summary>
    [CustomEditor(typeof(WeaponAuthoring))]
    [CanEditMultipleObjects]
    public class WeaponAuthoringEditor : AuthoringEditor
    {
        private void OnSceneGUI()
        {
            var weapon = (WeaponAuthoring)target;
            var center = weapon.transform.position;
            var self = RTSHandles.SelfRadius(weapon);

            if (weapon.acquireRange > 0f)
            {
                Handles.color = RTSHandles.Faded(Color.yellow);
                Handles.DrawWireDisc(center, Vector3.up, weapon.acquireRange);
            }

            EditorGUI.BeginChangeCheck();
            var reach = RTSHandles.Radius(center, weapon.range + self, Color.red, "Reach");
            if (Changed("Edit Weapon Range"))
            {
                weapon.range = Mathf.Max(0.1f, reach - self);
            }
        }
    }
}
