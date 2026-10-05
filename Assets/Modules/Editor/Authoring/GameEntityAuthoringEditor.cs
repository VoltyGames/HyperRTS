using HyperRTS.Editor.Validation;
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
        private string _summary;

        public override void OnInspectorGUI()
        {
            if (targets.Length == 1)
            {
                if (Event.current.type == EventType.Layout || _summary == null)
                {
                    _summary = EntitySummary.Line((GameEntityAuthoring)target);
                }

                EditorGUILayout.LabelField(_summary, EditorStyles.helpBox);
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
            var unit = entity as UnitAuthoring;
            var building = entity as BuildingAuthoring;
            var center = entity.transform.position;
            var color = SceneMatch.PlayerColor(entity.owner);

            EditorGUI.BeginChangeCheck();
            var vision = RTSHandles.Radius(center, entity.visionRange, RTSHandles.Faded(Color.white), "Vision");
            var radius = unit != null ? RTSHandles.Radius(center, unit.radius, color, "Radius") : 0f;
            var footprint = building != null ? RTSHandles.Footprint(center, building.footprint, color) : Vector2.zero;
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            QuickFixes.Edit(entity, "Edit " + entity.DisplayName, () =>
            {
                entity.visionRange = vision;
                if (unit != null)
                {
                    unit.radius = Mathf.Max(0.05f, radius);
                }

                if (building != null)
                {
                    building.footprint = footprint;
                }
            });
        }
    }
}
