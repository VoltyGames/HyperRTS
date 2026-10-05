using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>Per prefab: what it requires, what makes it, what it makes and what it unlocks.</summary>
    internal static class TechTreeView
    {
        public static void Draw(List<GameEntityAuthoring> visible, List<GameEntityAuthoring> all)
        {
            foreach (var prefab in visible)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button(prefab.DisplayName, EditorStyles.boldLabel))
                    {
                        UnityEditor.Selection.activeObject = prefab.gameObject;
                        EditorGUIUtility.PingObject(prefab.gameObject);
                    }

                    Line("Requires", prefab.prerequisites);
                    Line("Made by", all.Where(other => Makes(other).Contains(prefab)));
                    Line("Makes", Makes(prefab));
                    Line("Unlocks", all.Where(other => other.prerequisites.Contains(prefab)));
                }
            }
        }

        private static IEnumerable<GameEntityAuthoring> Makes(GameEntityAuthoring prefab)
        {
            var made = new List<GameEntityAuthoring>();
            if (prefab.TryGetComponent(out ProducerAuthoring producer))
            {
                made.AddRange(producer.productionOptions);
            }

            if (prefab.TryGetComponent(out BuilderAuthoring builder))
            {
                made.AddRange(builder.buildOptions);
            }

            return made.Where(option => option != null);
        }

        private static void Line(string label, IEnumerable<GameEntityAuthoring> prefabs)
        {
            var names = string.Join(", ", prefabs.Where(prefab => prefab != null).Select(prefab => prefab.DisplayName));
            if (names.Length > 0)
            {
                EditorGUILayout.LabelField(label, names, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
