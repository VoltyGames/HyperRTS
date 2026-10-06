using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Units;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Catalog
{
    /// <summary>HyperRTS ▸ Catalog: every unit and building prefab in one editable stats table, plus the tech tree.</summary>
    public class CatalogWindow : EditorWindow
    {
        private static readonly string[] Tabs = { "Stats", "Tech Tree" };
        private const float Narrow = 64f;

        private static readonly (string Header, System.Type Component, string Field)[] Columns =
        {
            ("HP", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.maxHealth)),
            ("Build s", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.buildTime)),
            ("Vision", typeof(GameEntityAuthoring), nameof(GameEntityAuthoring.visionRange)),
            ("Speed", typeof(UnitAuthoring), nameof(UnitAuthoring.moveSpeed)),
            ("Damage", typeof(WeaponAuthoring), nameof(WeaponAuthoring.damage)),
            ("Cooldown", typeof(WeaponAuthoring), nameof(WeaponAuthoring.cooldown)),
            ("Range", typeof(WeaponAuthoring), nameof(WeaponAuthoring.range)),
        };

        private List<GameEntityAuthoring> _prefabs = new();
        private readonly Dictionary<Component, SerializedObject> _serialized = new();
        private TechTreeView _techTree;
        private bool _stale = true;
        private int _tab;
        private string _filter = "";
        private Vector2 _scroll;

        [MenuItem(EditorMenu.Catalog, false, EditorMenu.CatalogPriority)]
        public static void Open() => GetWindow<CatalogWindow>("HyperRTS Catalog");

        // The prefab scan is a full project walk, so reload on the next draw instead of on every asset change.
        private void OnProjectChange() => _stale = true;

        private void OnDisable() => ClearSerialized();

        private void Reload()
        {
            ClearSerialized();
            _prefabs = EditorAssets.EntityPrefabs()
                .OrderBy(prefab => prefab is BuildingAuthoring)
                .ThenBy(prefab => prefab.DisplayName)
                .ToList();
            _stale = false;
        }

        private void OnGUI()
        {
            if (_stale)
            {
                Reload();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _tab = GUILayout.Toolbar(_tab, Tabs, EditorStyles.toolbarButton, GUILayout.Width(160));
                GUILayout.FlexibleSpace();
                _filter = EditorGUILayout.TextField(_filter, EditorStyles.toolbarSearchField, GUILayout.Width(200));
            }

            var visible = _prefabs.Where(MatchesFilter).ToList();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            if (_tab == 0)
            {
                DrawStats(visible);
            }
            else
            {
                // Rebuilt per layout (one pass over the prefabs) so inspector edits to options show up live.
                if (_techTree == null || Event.current.type == EventType.Layout)
                {
                    _techTree = new TechTreeView(_prefabs);
                }

                _techTree.Draw(visible);
            }

            EditorGUILayout.EndScrollView();
        }

        private bool MatchesFilter(GameEntityAuthoring prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            return prefab.DisplayName.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void DrawStats(List<GameEntityAuthoring> prefabs)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Header("Name", 140f);
                foreach (var column in Columns)
                {
                    Header(column.Header, Narrow);
                }

                Header("DPS", Narrow);
                GUILayout.Label("Cost", EditorStyles.miniBoldLabel);
            }

            foreach (var prefab in prefabs)
            {
                DrawRow(prefab);
            }
        }

        private void DrawRow(GameEntityAuthoring prefab)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button(prefab.DisplayName, EditorStyles.linkLabel, GUILayout.Width(140f)))
            {
                EditorAssets.Reveal(prefab.gameObject);
            }

            foreach (var column in Columns)
            {
                var component = prefab.GetComponent(column.Component);
                if (component == null)
                {
                    GUILayout.Label("-", GUILayout.Width(Narrow));
                    continue;
                }

                // PropertyField keeps the field's [Min] limits, undo and prefab overrides.
                var serialized = Serialized(component);
                serialized.Update();
                EditorGUILayout.PropertyField(serialized.FindProperty(column.Field), GUIContent.none, GUILayout.Width(Narrow));
                serialized.ApplyModifiedProperties();
            }

            var weapon = prefab.GetComponent<WeaponAuthoring>();
            GUILayout.Label(weapon != null ? EntitySummary.Dps(weapon).ToString("0.#") : "-", GUILayout.Width(Narrow));
            GUILayout.Label(EntitySummary.CostText(prefab), EditorStyles.miniLabel);
        }

        private SerializedObject Serialized(Component component)
        {
            if (!_serialized.TryGetValue(component, out var serialized))
            {
                _serialized[component] = serialized = new SerializedObject(component);
            }

            return serialized;
        }

        private void ClearSerialized()
        {
            foreach (var serialized in _serialized.Values)
            {
                serialized.Dispose();
            }

            _serialized.Clear();
        }

        private static void Header(string text, float width) =>
            GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));
    }
}
