using System;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.GameEntities;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Unit and building inspector: entity card, component strip, setup warnings and a draggable vision handle.</summary>
    [CustomEditor(typeof(GameEntityAuthoring), true)]
    [CanEditMultipleObjects]
    public class GameEntityAuthoringEditor : AuthoringEditor
    {
        private EntityCard _card;
        private ModuleStrip _modules;

        protected override void BuildHeader(VisualElement header)
        {
            if (targets.Length != 1)
            {
                return;
            }

            EditorAssets.Layout(typeof(GameEntityAuthoringEditor)).CloneTree(header);
            _card = header.Require<EntityCard>("card");
            _modules = header.Require<ModuleStrip>("modules");
        }

        protected override void Refresh()
        {
            if (_card != null)
            {
                var entity = (GameEntityAuthoring)target;
                _card.Show(entity);
                _modules.Show(entity.gameObject);
            }
        }

        /// <summary>Draws the entity's shape handle; returns the edit to apply when it was dragged, if any.</summary>
        protected virtual Action ShapeHandle(Vector3 center, Color color) => null;

        private void OnSceneGUI()
        {
            var entity = (GameEntityAuthoring)target;
            var center = entity.transform.position;

            EditorGUI.BeginChangeCheck();
            var vision = GroundHandles.Radius(center, entity.visionRange,
                new GroundHandles.RadiusDot(GroundHandles.Faded(Color.white), "Vision", GroundHandles.VisionAngle));
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
