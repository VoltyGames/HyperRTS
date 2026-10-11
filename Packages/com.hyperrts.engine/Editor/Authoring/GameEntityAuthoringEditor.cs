using System;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Unit and building inspector: summary, setup warnings and a draggable vision handle.</summary>
    [CustomEditor(typeof(GameEntityAuthoring), true)]
    [CanEditMultipleObjects]
    public class GameEntityAuthoringEditor : AuthoringEditor
    {
        private HelpBox _summary;

        protected override void BuildHeader(VisualElement root)
        {
            if (targets.Length != 1)
            {
                return;
            }

            _summary = new HelpBox("", HelpBoxMessageType.None);
            _summary.AddToClassList("hrts-summary");
            root.Add(_summary);
        }

        protected override void Refresh()
        {
            if (_summary != null)
            {
                _summary.text = EntitySummary.Line((GameEntityAuthoring)target);
            }
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

            EditorUndo.Record(entity, "Edit " + entity.DisplayName, () =>
            {
                entity.visionRange = vision;
                applyShape?.Invoke();
            });
        }
    }
}
