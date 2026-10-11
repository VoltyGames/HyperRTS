using System;
using System.Collections.Generic;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>The catalog's stats tab: one row per prefab, with its numbers edited in place.</summary>
    internal sealed class StatsTable : MultiColumnListView
    {
        private const float NameWidth = 140f;
        private const float Narrow = 64f;

        private static readonly (string Header, Type Component, string Field)[] Fields =
        {
            ("HP", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.maxHealth)),
            ("Build s", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.buildTime)),
            ("Vision", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.visionRange)),
            ("Speed", typeof(UnitAuthoring), nameof(UnitAuthoring.moveSpeed)),
            ("Damage", typeof(WeaponAuthoring), nameof(WeaponAuthoring.damage)),
            ("Cooldown", typeof(WeaponAuthoring), nameof(WeaponAuthoring.cooldown)),
            ("Range", typeof(WeaponAuthoring), nameof(WeaponAuthoring.range)),
        };

        private readonly Dictionary<Component, SerializedObject> _serialized = new();

        public StatsTable()
        {
            AddToClassList("hrts-fill");
            fixedItemHeight = 22f;
            showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            columns.Add(NameColumn());
            foreach (var field in Fields)
            {
                columns.Add(FieldColumn(field.Header, field.Component, field.Field));
            }

            columns.Add(DpsColumn());
            columns.Add(new Column
            {
                title = "Cost",
                stretchable = true,
                makeCell = () => Cell("-"),
                bindCell = (cell, index) => ((Label)cell).text = EntitySummary.CostText(Prefab(index)),
            });
        }

        public void Show(List<GameEntityAuthoring> prefabs)
        {
            itemsSource = prefabs;
            Rebuild();
        }

        /// <summary>Frees the cached serialized objects; call when the window closes.</summary>
        public void Release()
        {
            itemsSource = null;
            Rebuild();
            foreach (var serialized in _serialized.Values)
            {
                serialized.Dispose();
            }

            _serialized.Clear();
        }

        private GameEntityAuthoring Prefab(int index) => (GameEntityAuthoring)itemsSource[index];

        private Column NameColumn() => new()
        {
            title = "Name",
            width = NameWidth,
            makeCell = () =>
            {
                var button = new Button();
                button.clicked += () => EditorAssets.Reveal(((Component)button.userData).gameObject);
                return button;
            },
            bindCell = (cell, index) =>
            {
                var button = (Button)cell;
                button.userData = Prefab(index);
                button.text = Prefab(index).DisplayName;
            },
        };

        // PropertyField keeps the field's [Min] limits, undo and prefab overrides.
        private Column FieldColumn(string header, Type type, string field) => new()
        {
            title = header,
            width = Narrow,
            makeCell = () => new VisualElement(),
            bindCell = (cell, index) =>
            {
                var component = Prefab(index).GetComponent(type);
                if (component == null)
                {
                    cell.Add(Cell("-"));
                    return;
                }

                var serialized = Serialized(component);
                var property = new PropertyField(serialized.FindProperty(field), "");
                property.AddToClassList("hrts-table__cell");
                property.Bind(serialized);
                cell.Add(property);
            },
            unbindCell = (cell, _) =>
            {
                cell.Unbind();
                cell.Clear();
            },
        };

        private Column DpsColumn() => new()
        {
            title = "DPS",
            width = Narrow,
            makeCell = () => Cell("-"),
            bindCell = (cell, index) =>
            {
                var label = (Label)cell;
                var weapon = Prefab(index).GetComponent<WeaponAuthoring>();
                label.text = Dps(weapon);
                if (weapon != null)
                {
                    label.TrackSerializedObjectValue(Serialized(weapon), _ => label.text = Dps(weapon));
                }
            },
            unbindCell = (cell, _) => cell.Unbind(),
        };

        private SerializedObject Serialized(Component component)
        {
            if (!_serialized.TryGetValue(component, out var serialized))
            {
                _serialized[component] = serialized = new SerializedObject(component);
            }

            return serialized;
        }

        private static string Dps(WeaponAuthoring weapon) => weapon != null ? EntitySummary.Dps(weapon).ToString("0.#") : "-";

        private static Label Cell(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hrts-table__cell");
            return label;
        }
    }
}
