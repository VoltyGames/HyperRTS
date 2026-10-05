using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Authoring;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
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

        private List<GameEntityAuthoring> _prefabs = new();
        private TechTreeView _techTree;
        private bool _stale = true;
        private int _tab;
        private string _filter = "";
        private Vector2 _scroll;

        [MenuItem("HyperRTS/Catalog", false, 21)]
        public static void Open() => GetWindow<CatalogWindow>("HyperRTS Catalog");

        // The prefab scan is a full project walk, so reload on the next draw instead of on every asset change.
        private void OnProjectChange() => _stale = true;

        private void Reload()
        {
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

            var visible = _prefabs.Where(prefab => prefab != null &&
                prefab.DisplayName.IndexOf(_filter, System.StringComparison.OrdinalIgnoreCase) >= 0).ToList();

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

        private static void DrawStats(List<GameEntityAuthoring> prefabs)
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                Header("Name", 140f);
                foreach (var column in new[] { "HP", "Build s", "Vision", "Speed", "Damage", "Cooldown", "DPS", "Range" })
                {
                    Header(column, Narrow);
                }

                GUILayout.Label("Cost", EditorStyles.miniBoldLabel);
            }

            foreach (var prefab in prefabs)
            {
                DrawRow(prefab);
            }
        }

        private static void DrawRow(GameEntityAuthoring prefab)
        {
            using var row = new EditorGUILayout.HorizontalScope();
            if (GUILayout.Button(prefab.DisplayName, EditorStyles.linkLabel, GUILayout.Width(140f)))
            {
                EditorAssets.Reveal(prefab.gameObject);
            }

            prefab.maxHealth = Field(prefab, prefab.maxHealth);
            prefab.buildTime = Field(prefab, prefab.buildTime);
            prefab.visionRange = Field(prefab, prefab.visionRange);

            if (prefab is UnitAuthoring unit)
            {
                unit.moveSpeed = Field(unit, unit.moveSpeed);
            }
            else
            {
                GUILayout.Label("-", GUILayout.Width(Narrow));
            }

            DrawWeapon(prefab.GetComponent<WeaponAuthoring>());
            GUILayout.Label(EntitySummary.CostText(prefab), EditorStyles.miniLabel);
        }

        private static void DrawWeapon(WeaponAuthoring weapon)
        {
            if (weapon == null)
            {
                for (var i = 0; i < 4; i++)
                {
                    GUILayout.Label("-", GUILayout.Width(Narrow));
                }

                return;
            }

            weapon.damage = Field(weapon, weapon.damage);
            weapon.cooldown = Mathf.Max(0.05f, Field(weapon, weapon.cooldown));
            GUILayout.Label(EntitySummary.Dps(weapon).ToString("0.#"), GUILayout.Width(Narrow));
            weapon.range = Field(weapon, weapon.range);
        }

        private static float Field(Object target, float value)
        {
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.DelayedFloatField(value, GUILayout.Width(Narrow));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(target, "Edit Stats");
                EditorUtility.SetDirty(target);
            }

            return next;
        }

        private static void Header(string text, float width) =>
            GUILayout.Label(text, EditorStyles.miniBoldLabel, GUILayout.Width(width));
    }
}
