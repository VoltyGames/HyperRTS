using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
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
            var self = weapon.TryGetComponent(out GameEntityAuthoring entity) ? entity.Radius : EntityRadius.Default;

            if (weapon.acquireRange > 0f)
            {
                Handles.color = GroundHandles.Faded(Color.yellow);
                Handles.DrawWireDisc(center, Vector3.up, weapon.acquireRange);
            }

            GroundHandles.EditRadius(weapon, center, weapon.range + self, Color.red, "Reach", "Edit Weapon Range",
                reach => weapon.range = Mathf.Max(0.1f, reach - self));
        }
    }
}
