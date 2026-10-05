using System;
using HyperRTS.Editor.Validation;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Unit and building inspector: summary, setup warnings and a draggable vision handle.</summary>
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

        /// <summary>Draws the entity's shape handle; returns the edit to apply when it was dragged, if any.</summary>
        protected virtual Action ShapeHandle(Vector3 center, Color color) => null;

        private void OnSceneGUI()
        {
            var entity = (GameEntityAuthoring)target;
            var center = entity.transform.position;

            EditorGUI.BeginChangeCheck();
            var vision = GroundHandles.Radius(center, entity.visionRange, GroundHandles.Faded(Color.white), "Vision");
            var applyShape = ShapeHandle(center, SceneMatch.PlayerColor(entity.owner));
            if (!EditorGUI.EndChangeCheck())
            {
                return;
            }

            QuickFixes.Edit(entity, "Edit " + entity.DisplayName, () =>
            {
                entity.visionRange = vision;
                applyShape?.Invoke();
            });
        }
    }
}
