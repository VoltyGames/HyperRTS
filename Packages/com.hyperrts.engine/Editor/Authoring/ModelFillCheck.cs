using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Authoring
{
    /// <summary>Finds a component's first value off from what its model gives, filling it and putting everything back.</summary>
    public sealed class ModelFillCheck
    {
        // Float rounding alone moves a refilled value by less than this.
        private const float Tolerance = 1e-4f;

        private readonly Component _component;

        public ModelFillCheck(Component component) => _component = component;

        /// <summary>
        /// The property path of the first value off from the model, or null when it matches. Throws as the fill does when
        /// the model lacks a part.
        /// </summary>
        public string FirstDifference()
        {
            var current = new SerializedObject(_component);
            var filled = Filled();
            var mine = current.GetIterator();
            var model = filled.GetIterator();
            var enter = true;
            while (mine.NextVisible(enter))
            {
                if (!model.NextVisible(enter) || mine.propertyPath != model.propertyPath)
                {
                    return mine.propertyPath;
                }

                enter = mine.hasVisibleChildren;
                if (!enter && !Same(mine, model))
                {
                    return mine.propertyPath;
                }
            }

            return model.NextVisible(enter) ? model.propertyPath : null;
        }

        // A SerializedObject keeps the values it read until Update, so it outlives the restore. A fill may also set
        // the component's siblings, such as weapon pivots, hide or move the models below it, and add components there.
        private SerializedObject Filled()
        {
            var siblings = _component.GetComponents<Component>().Where(sibling => sibling != null && sibling is not Transform).ToList();
            var saved = siblings.ConvertAll(EditorJsonUtility.ToJson);
            var before = new HashSet<Component>(_component.GetComponentsInChildren<Component>(true));
            var placed = new TransformSnapshot(_component.transform);
            try
            {
                ModelFillCommands.Fill(_component);
                return new SerializedObject(_component);
            }
            finally
            {
                for (var i = 0; i < siblings.Count; i++)
                {
                    EditorJsonUtility.FromJsonOverwrite(saved[i], siblings[i]);
                }

                placed.Restore();

                foreach (var added in _component.GetComponentsInChildren<Component>(true).Where(added => !before.Contains(added)))
                {
                    Object.DestroyImmediate(added, true);
                }
            }
        }

        private static bool Same(SerializedProperty mine, SerializedProperty model) =>
            mine.propertyType == SerializedPropertyType.Float
                ? Mathf.Abs(mine.floatValue - model.floatValue) <= Tolerance
                : SerializedProperty.DataEquals(mine, model);
    }
}
