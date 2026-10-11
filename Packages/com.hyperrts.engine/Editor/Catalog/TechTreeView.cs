using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Production;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>Per prefab: what it requires, what makes it, what it makes and what it unlocks.</summary>
    internal sealed class TechTreeView : VisualElement
    {
        private readonly Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> _makes = new();
        private readonly Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> _madeBy = new();
        private readonly Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> _unlocks = new();

        /// <summary>Rebuilds the links from <paramref name="all"/> and shows the <paramref name="visible"/> ones.</summary>
        public void Show(IEnumerable<GameEntityAuthoring> all, IEnumerable<GameEntityAuthoring> visible)
        {
            Link(all);
            Clear();
            foreach (var prefab in visible)
            {
                Add(Node(prefab));
            }
        }

        private void Link(IEnumerable<GameEntityAuthoring> all)
        {
            _makes.Clear();
            _madeBy.Clear();
            _unlocks.Clear();
            foreach (var prefab in all.Where(prefab => prefab != null))
            {
                _makes[prefab] = Makes(prefab);
                foreach (var made in _makes[prefab])
                {
                    Append(_madeBy, made, prefab);
                }

                foreach (var prerequisite in prefab.prerequisites.Where(prerequisite => prerequisite != null))
                {
                    Append(_unlocks, prerequisite, prefab);
                }
            }
        }

        private VisualElement Node(GameEntityAuthoring prefab)
        {
            var node = new VisualElement();
            node.AddToClassList("hrts-tree__node");
            var name = new Button(() => EditorAssets.Reveal(prefab.gameObject)) { text = prefab.DisplayName };
            name.AddToClassList("hrts-tree__name");
            node.Add(name);
            AddLine(node, "Requires", prefab.prerequisites);
            AddLine(node, "Made by", _madeBy.GetValueOrDefault(prefab));
            AddLine(node, "Makes", _makes.GetValueOrDefault(prefab));
            AddLine(node, "Unlocks", _unlocks.GetValueOrDefault(prefab));
            return node;
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

        private static void Append(Dictionary<GameEntityAuthoring, List<GameEntityAuthoring>> map,
            GameEntityAuthoring key, GameEntityAuthoring value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                map[key] = list = new List<GameEntityAuthoring>();
            }

            list.Add(value);
        }

        private static void AddLine(VisualElement node, string label, IEnumerable<GameEntityAuthoring> prefabs)
        {
            if (prefabs == null)
            {
                return;
            }

            var names = string.Join(", ", prefabs.Where(prefab => prefab != null).Select(prefab => prefab.DisplayName));
            if (names.Length > 0)
            {
                var line = new Label($"{label}: {names}");
                line.AddToClassList("hrts-tree__line");
                node.Add(line);
            }
        }
    }
}
