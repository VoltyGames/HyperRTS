using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>
    /// A searchable list of the spawnable prefabs that reports the chosen <c>EntityInfo.TypeId</c>: units, buildings and
    /// other, under the headings of the game's <see cref="ISpawnGrouping"/> when it has one.
    /// </summary>
    internal sealed class SpawnPicker : AdvancedDropdown
    {
        private const string OtherHeading = "Other";

        private readonly List<Choice> _choices;
        private readonly IReadOnlyList<string> _headings;
        private readonly Action<int> _picked;

        private SpawnPicker(List<Choice> choices, IReadOnlyList<string> headings, Action<int> picked)
            : base(new AdvancedDropdownState())
        {
            _choices = choices;
            _headings = headings;
            _picked = picked;
            minimumSize = new Vector2(260f, 360f);
        }

        /// <summary>Opens the list under <paramref name="below"/>, a panel rect.</summary>
        public static void Open(EntityManager entityManager, Rect below, Action<int> picked)
        {
            var grouping = TypeDiscovery.Instances<ISpawnGrouping>().FirstOrDefault();
            var headings = grouping?.Headings ?? new List<string>();
            var choices = new List<Choice>();
            foreach (var prefab in SpawnCheat.Prefabs(entityManager))
            {
                var info = entityManager.GetComponentData<EntityInfo>(prefab);
                var name = info.Name.ToString();
                var heading = grouping?.FindHeading(name);
                choices.Add(new Choice(info.TypeId, name, FindKind(entityManager, prefab), HeadingIndex(headings, heading)));
            }

            new SpawnPicker(choices, headings, picked).Show(below);
        }

        /// <summary>The prefab's name for the picker button, or null when it is gone.</summary>
        public static string FindName(EntityManager entityManager, int typeId)
        {
            var prefab = SpawnCheat.FindPrefab(entityManager, typeId);
            return prefab == Entity.Null ? null : entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString();
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Spawn");
            if (_headings.Count == 0)
            {
                AddKinds(root, _choices);
                return root;
            }

            foreach (var group in _choices.GroupBy(choice => choice.Heading).OrderBy(group => group.Key))
            {
                var heading = new AdvancedDropdownItem(group.Key < _headings.Count ? _headings[group.Key] : OtherHeading);
                AddKinds(heading, group);
                root.AddChild(heading);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item) => _picked(item.id);

        private static void AddKinds(AdvancedDropdownItem parent, IEnumerable<Choice> choices)
        {
            foreach (var group in choices.GroupBy(choice => choice.Kind).OrderBy(group => group.Key))
            {
                var folder = new AdvancedDropdownItem(group.Key.ToString());
                foreach (var choice in group.OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase))
                {
                    // AddChild overwrites the id with one made from the name, so the type id goes on after it.
                    var item = new AdvancedDropdownItem(choice.Name);
                    folder.AddChild(item);
                    item.id = choice.TypeId;
                }

                parent.AddChild(folder);
            }
        }

        private static int HeadingIndex(IReadOnlyList<string> headings, string heading)
        {
            if (heading == null)
            {
                return headings.Count;
            }

            for (var i = 0; i < headings.Count; i++)
            {
                if (headings[i] == heading)
                {
                    return i;
                }
            }

            throw new InvalidOperationException($"[HyperRTS] The spawn grouping gave the heading '{heading}', which is not in its Headings.");
        }

        private static Kind FindKind(EntityManager entityManager, Entity prefab)
        {
            if (entityManager.HasComponent<UnitTag>(prefab))
            {
                return Kind.Units;
            }

            return entityManager.HasComponent<BuildingTag>(prefab) ? Kind.Buildings : Kind.Other;
        }

        // Listed in this order: units first, as they are spawned most.
        private enum Kind
        {
            Units,
            Buildings,
            Other,
        }

        private readonly struct Choice
        {
            public readonly int TypeId;
            public readonly string Name;
            public readonly Kind Kind;

            /// <summary>Index in the grouping's headings, or past them for Other.</summary>
            public readonly int Heading;

            public Choice(int typeId, string name, Kind kind, int heading)
            {
                TypeId = typeId;
                Name = name;
                Kind = kind;
                Heading = heading;
            }
        }
    }
}
