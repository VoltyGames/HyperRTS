using HyperRTS.Simulation.Combat.Damage;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Stats;
using System;
using System.Collections.Generic;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Common;
using HyperRTS.Presentation.HUD;
using HyperRTS.Simulation.GameEntities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>The catalog's stats tab: one row per prefab, with its numbers edited in place.</summary>
    [UxmlElement]
    public sealed partial class StatsTable : MultiColumnListView
    {
        private const float NameWidth = 190f;
        private const float Narrow = 76f;

        [NoAutoStaticsCleanup]
        private static readonly (string Header, Type Component, string Field)[] Fields =
        {
            ("HP", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.maxHealth)),
            ("Build s", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.buildTime)),
            ("Vision", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.visionRange)),
            ("Speed", typeof(UnitAuthoring), nameof(UnitAuthoring.moveSpeed)),
            ("Damage", typeof(WeaponAuthoring), Stat($"{nameof(WeaponStats.hit)}.{nameof(DamageSettings.damage)}")),
            ("Cooldown", typeof(WeaponAuthoring), Stat(nameof(WeaponStats.cooldown))),
            ("Range", typeof(WeaponAuthoring), Stat(nameof(WeaponStats.range))),
        };

        private readonly Dictionary<Component, SerializedObject> _serialized = new();

        public StatsTable()
        {
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
                button.AddToClassList("hrts-table__name");
                button.clicked += () => EditorAssets.Reveal(((Component)button.userData).gameObject);
                return button;
            },
            bindCell = (cell, index) =>
            {
                var button = (Button)cell;
                button.userData = Prefab(index);
                button.text = Prefab(index).DisplayName;
                button.tooltip = button.text;
            },
        };

        // PropertyField keeps the field's [Min] limits, undo and prefab overrides.
        private Column FieldColumn(string header, Type type, string field) => new()
        {
            title = header,
            width = Narrow,
            // One field a cell, rebound as rows scroll: building a PropertyField a bind left the cells filling late.
            makeCell = () =>
            {
                var cell = new VisualElement();
                cell.Add(Cell("-"));
                var property = new PropertyField { label = "" };
                property.AddToClassList("hrts-table__cell");
                cell.Add(property);
                return cell;
            },
            bindCell = (cell, index) =>
            {
                var component = Prefab(index).GetComponent(type);
                var property = cell.Q<PropertyField>();
                cell.Q<Label>().SetHidden(component != null);
                property.SetHidden(component == null);
                if (component != null)
                {
                    property.BindProperty(Serialized(component).FindProperty(field));
                }
            },
            unbindCell = (cell, _) => cell.Q<PropertyField>().Unbind(),
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

        private static string Stat(string field) => $"{nameof(WeaponAuthoring.stats)}.{field}";

        private static string Dps(WeaponAuthoring weapon) => weapon != null ? EntitySummary.Dps(weapon).ToString("0.#") : "-";

        private static Label Cell(string text)
        {
            var label = new Label(text);
            label.AddToClassList("hrts-table__cell");
            return label;
        }
    }
}
