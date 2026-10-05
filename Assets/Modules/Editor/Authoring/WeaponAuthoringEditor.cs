using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
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
            var self = weapon.TryGetComponent(out GameEntityAuthoring owner) ? owner.FootprintRadius : Footprint.DefaultRadius;

            if (weapon.acquireRange > 0f)
            {
                Handles.color = RTSHandles.Faded(Color.yellow);
                Handles.DrawWireDisc(center, Vector3.up, weapon.acquireRange);
            }

            EditorGUI.BeginChangeCheck();
            var reach = RTSHandles.Radius(center, weapon.range + self, Color.red, "Reach");
            if (EditorGUI.EndChangeCheck())
            {
                QuickFixes.Edit(weapon, "Edit Weapon Range", () => weapon.range = Mathf.Max(0.1f, reach - self));
            }
        }
    }
}
