using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>Per prefab: what it requires, what makes it, what it makes and what it unlocks.</summary>
    internal class TechTreeView
    {
        private readonly Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> _makes = new();
        private readonly Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> _madeBy = new();
        private readonly Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> _unlocks = new();

        public TechTreeView(List<GameEntityAuthoring> all)
        {
            foreach (var prefab in all.Where(prefab => prefab != null))
            {
                _makes[prefab] = Makes(prefab);
                foreach (var made in _makes[prefab])
                {
                    Add(_madeBy, made, prefab);
                }

                foreach (var prerequisite in prefab.prerequisites.Where(prerequisite => prerequisite != null))
                {
                    Add(_unlocks, prerequisite, prefab);
                }
            }
        }

        public void Draw(List<GameEntityAuthoring> visible)
        {
            foreach (var prefab in visible)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    if (GUILayout.Button(prefab.DisplayName, EditorStyles.boldLabel))
                    {
                        EditorAssets.Reveal(prefab.gameObject);
                    }

                    Line("Requires", prefab.prerequisites);
                    Line("Made by", _madeBy.GetValueOrDefault(prefab));
                    Line("Makes", _makes.GetValueOrDefault(prefab));
                    Line("Unlocks", _unlocks.GetValueOrDefault(prefab));
                }
            }
        }

        private static List<GameEntityAuthoring> Makes(GameEntityAuthoring prefab)
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

            made.RemoveAll(option => option == null);
            return made;
        }

        private static void Add(Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> map, GameEntityAuthoring key,
            GameEntityAuthoring value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = new List<GameEntityAuthoring>();
            }

            list.Add(value);
        }

        private static void Line(string label, IEnumerable<GameEntityAuthoring> prefabs)
        {
            if (prefabs == null)
            {
                return;
            }

            var names = string.Join(", ", prefabs.Where(prefab => prefab != null).Select(prefab => prefab.DisplayName));
            if (names.Length > 0)
            {
                EditorGUILayout.LabelField(label, names, EditorStyles.wordWrappedLabel);
            }
        }
    }
}
