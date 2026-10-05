using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Unit and building inspector: setup warnings plus draggable radius, footprint and vision handles.</summary>
    [CustomEditor(typeof(GameEntityAuthoring), true)]
    [CanEditMultipleObjects]
    public class GameEntityAuthoringEditor : AuthoringEditor
    {
        public override void OnInspectorGUI()
        {
            if (targets.Length == 1)
            {
                EditorGUILayout.LabelField(EntitySummary.Line((GameEntityAuthoring)target), EditorStyles.helpBox);
            }

            base.OnInspectorGUI();
        }

        protected override bool DrawProperty(SerializedProperty property)
        {
            if (property.name != nameof(GameEntityAuthoring.owner))
            {
                return false;
            }

            OwnerField.Draw(property);
            return true;
        }

        private void OnSceneGUI()
        {
            var entity = (GameEntityAuthoring)target;
            var center = entity.transform.position;
            var color = RTSHandles.TeamColor(entity);

            EditorGUI.BeginChangeCheck();
            var vision = RTSHandles.Radius(center, entity.visionRange, RTSHandles.Faded(Color.white), "Vision");
            var radius = entity is UnitAuthoring unit ? RTSHandles.Radius(center, unit.radius, color, "Radius") : 0f;
            var footprint = entity is BuildingAuthoring building
                ? RTSHandles.Footprint(center, building.footprint, color)
                : Vector2.zero;

            if (!Changed("Edit " + entity.DisplayName))
            {
                return;
            }

            entity.visionRange = vision;
            if (entity is UnitAuthoring editedUnit)
            {
                editedUnit.radius = Mathf.Max(0.05f, radius);
            }
            else if (entity is BuildingAuthoring editedBuilding)
            {
                editedBuilding.footprint = footprint;
            }
        }
    }
}
