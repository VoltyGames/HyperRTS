using System;
using System.Collections.Generic;
using HyperRTS.Simulation.Production;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Decorations
{
    /// <summary>Draws a unit prefab's command card icon over its thumbnail in the Project window's grid view, found once and dropped when its asset changes.</summary>
    internal sealed class ProjectUnitIcons
    {
        private const float LabelHeight = 16f;
        private const float BadgeSize = 16f;

        private readonly Dictionary<string, Texture2D> _icons = new();
        private Texture _prefabBadge;

        [InitializeOnLoadMethod]
        private static void Install()
        {
            var icons = new ProjectUnitIcons();
            EditorApplication.projectWindowItemOnGUI += icons.Draw;
            ChangedAssets.Subscribe(icons.Forget);
        }

        // A deleted asset has no GUID left to look up; nothing draws it either.
        private void Forget(string[] paths)
        {
            foreach (var path in paths)
            {
                _icons.Remove(AssetDatabase.AssetPathToGUID(path));
            }
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
            if (!DecorationSettings.instance.projectUnitIcons || row.height <= DecorationStyle.ListRowHeight)
            {
                return;
            }

            var icon = FindIcon(guid);
            if (icon == null)
            {
                return;
            }

            var thumbnail = new Rect(row.x, row.y, row.width, row.height - LabelHeight);
            GUI.DrawTexture(thumbnail, icon, ScaleMode.ScaleToFit);
            var badge = new Rect(thumbnail.xMax - BadgeSize, thumbnail.yMax - BadgeSize, BadgeSize, BadgeSize);
            if (_prefabBadge == null)
            {
                _prefabBadge = EditorGUIUtility.IconContent("Prefab Icon").image;
            }

            GUI.DrawTexture(badge, _prefabBadge, ScaleMode.ScaleToFit);
        }

        private static Texture2D LoadIcon(string path)
        {
            if (!path.EndsWith(".prefab", StringComparison.Ordinal))
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
