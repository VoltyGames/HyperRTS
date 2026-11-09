using System;
using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.Common;
using Unity.Entities;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Cheats
{
    /// <summary>A searchable list of the spawnable prefabs, in units, buildings and other, that reports the chosen <c>EntityInfo.TypeId</c>.</summary>
    internal sealed class SpawnPicker : AdvancedDropdown
    {
        private readonly List<Choice> _choices;
        private readonly Action<int> _picked;

        private SpawnPicker(List<Choice> choices, Action<int> picked) : base(new AdvancedDropdownState())
        {
            _choices = choices;
            _picked = picked;
            minimumSize = new Vector2(260f, 360f);
        }

        /// <summary>Opens the list under <paramref name="below"/>, a panel rect.</summary>
        public static void Open(EntityManager entityManager, Rect below, Action<int> picked)
        {
            var choices = new List<Choice>();
            foreach (var prefab in MatchCheats.Prefabs(entityManager))
            {
                var info = entityManager.GetComponentData<EntityInfo>(prefab);
                choices.Add(new Choice(info.TypeId, info.Name.ToString(), FindGroup(entityManager, prefab)));
            }

            new SpawnPicker(choices, picked).Show(below);
        }

        /// <summary>The prefab's name for the picker button, or null when it is gone.</summary>
        public static string FindName(EntityManager entityManager, int typeId)
        {
            var prefab = MatchCheats.FindPrefab(entityManager, typeId);
            return prefab == Entity.Null ? null : entityManager.GetComponentData<EntityInfo>(prefab).Name.ToString();
        }

        protected override AdvancedDropdownItem BuildRoot()
        {
            var root = new AdvancedDropdownItem("Spawn");
            foreach (var group in _choices.GroupBy(choice => choice.Group).OrderBy(group => group.Key))
            {
                var folder = new AdvancedDropdownItem(group.Key.ToString());
                foreach (var choice in group.OrderBy(choice => choice.Name, StringComparer.OrdinalIgnoreCase))
                {
                    folder.AddChild(new AdvancedDropdownItem(choice.Name) { id = choice.TypeId });
                }

                root.AddChild(folder);
            }

            return root;
        }

        protected override void ItemSelected(AdvancedDropdownItem item) => _picked(item.id);

        private static Group FindGroup(EntityManager entityManager, Entity prefab)
        {
            if (entityManager.HasComponent<UnitTag>(prefab))
            {
                return Group.Units;
            }

            return entityManager.HasComponent<BuildingTag>(prefab) ? Group.Buildings : Group.Other;
        }

        // Listed in this order: units first, as they are spawned most.
        private enum Group
        {
            Units,
            Buildings,
            Other,
        }

        private readonly struct Choice
        {
            public readonly int TypeId;
            public readonly string Name;
            public readonly Group Group;

            public Choice(int typeId, string name, Group group)
            {
                TypeId = typeId;
                Name = name;
                Group = group;
            }
        }
    }
}
