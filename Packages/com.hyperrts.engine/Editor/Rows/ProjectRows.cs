using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Editor.Generators;
using HyperRTS.Simulation.Production;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Rows
{
    /// <summary>
    /// Draws a unit prefab's command card icon over its thumbnail in the Project window's grid view, and a dot on the
    /// entry of a generator item that is out of date or failed. Both are found once and kept until the project changes.
    /// </summary>
    internal sealed class ProjectRows
    {
        private const float ListRowHeight = 20f;
        private const float LabelHeight = 16f;
        private const float BadgeSize = 16f;
        private const float DotSize = 6f;
        private const float DotMargin = 3f;

        // The Hierarchy rows' warning and error dot colours.
        [NoAutoStaticsCleanup]
        private static readonly Color OutOfDateColor = new Color32(196, 136, 0, 255);

        [NoAutoStaticsCleanup]
        private static readonly Color FailedColor = new Color32(222, 64, 52, 255);

        private readonly Dictionary<string, Texture2D> _icons = new();
        private Dictionary<string, Color> _itemDots;
        private Texture _prefabBadge;

        [InitializeOnLoadMethod]
        private static void Install()
        {
            var rows = new ProjectRows();
            EditorApplication.projectWindowItemOnGUI += rows.Draw;
            EditorApplication.projectChanged += rows.Clear;
            GeneratorRunLog.Recorded += rows.ClearItemDots;
        }

        private void Clear()
        {
            _icons.Clear();
            _itemDots = null;
        }

        private void ClearItemDots()
        {
            _itemDots = null;
            EditorApplication.RepaintProjectWindow();
        }

        private Texture2D FindIcon(string guid)
        {
            if (_icons.TryGetValue(guid, out var icon))
            {
                return icon;
            }

            icon = LoadIcon(AssetDatabase.GUIDToAssetPath(guid));
            _icons[guid] = icon;
            return icon;
        }

        private void Draw(string guid, Rect row)
        {
            if (EditorState.instance.projectUnitIcons && row.height > ListRowHeight)
            {
                DrawIcon(guid, row);
            }

            if (EditorState.instance.projectGeneratorStates)
            {
                DrawItemDot(guid, row);
            }
        }

        private void DrawIcon(string guid, Rect row)
        {
            var icon = FindIcon(guid);
            if (icon == null)
            {
                return;
            }

            var thumbnail = new Rect(row.x, row.y, row.width, row.height - LabelHeight);
            GUI.DrawTexture(thumbnail, icon, ScaleMode.ScaleToFit);
            var badge = new Rect(thumbnail.xMax - BadgeSize, thumbnail.yMax - BadgeSize, BadgeSize, BadgeSize);
            _prefabBadge ??= EditorGUIUtility.IconContent("Prefab Icon").image;
            GUI.DrawTexture(badge, _prefabBadge, ScaleMode.ScaleToFit);
        }

        private void DrawItemDot(string guid, Rect row)
        {
            _itemDots ??= FindItemDots();
            if (!_itemDots.TryGetValue(guid, out var color))
            {
                return;
            }

            // The list view centres the dot on its line; the grid view puts it in the thumbnail's corner.
            var y = row.height > ListRowHeight ? row.y + DotMargin : row.y + (row.height - DotSize) / 2f;
            var dot = new Rect(row.xMax - DotSize - DotMargin, y, DotSize, DotSize);
            GUI.DrawTexture(dot, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, DotSize / 2f);
        }

        // Every item's state once, keyed by the GUID of its project path; current items get no entry.
        private static Dictionary<string, Color> FindItemDots()
        {
            var dots = new Dictionary<string, Color>();
            var log = GeneratorRunLog.Load();
            foreach (var generator in GeneratorRunner.All())
            {
                foreach (var item in generator.Items)
                {
                    if (!generator.IsOutOfDate(item, log))
                    {
                        continue;
                    }

                    // Items sharing an entry show the worst state.
                    var guid = AssetDatabase.AssetPathToGUID(item.ProjectPath);
                    var failed = log.Find(generator.Name, item.Name) is { Failed: true };
                    if (failed || !dots.ContainsKey(guid))
                    {
                        dots[guid] = failed ? FailedColor : OutOfDateColor;
                    }
                }
            }

            return dots;
        }

        private static Texture2D LoadIcon(string path)
        {
            if (!path.EndsWith(".prefab"))
            {
                return null;
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root == null || !root.TryGetComponent<ProducibleAuthoring>(out var producible))
            {
                return null;
            }

            return producible.icon;
        }
    }
}
