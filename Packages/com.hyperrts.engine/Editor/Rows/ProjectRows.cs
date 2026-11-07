using System.Collections.Generic;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Production;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Rows
{
    /// <summary>
    /// Draws a unit prefab's command card icon over its thumbnail in the Project window's grid view. Icons are
    /// found once per asset and kept until the project changes.
    /// </summary>
    internal sealed class ProjectRows
    {
        private const float ListRowHeight = 20f;
        private const float LabelHeight = 16f;
        private const float BadgeSize = 16f;

        private readonly Dictionary<string, Texture2D> _icons = new();

        [InitializeOnLoadMethod]
        private static void Install()
        {
            var rows = new ProjectRows();
            EditorApplication.projectWindowItemOnGUI += rows.Draw;
            EditorApplication.projectChanged += rows.Clear;
        }

        public void Clear() => _icons.Clear();

        public Texture2D FindIcon(string guid)
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
            if (!EditorState.instance.projectUnitIcons || row.height <= ListRowHeight)
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
            GUI.DrawTexture(badge, EditorGUIUtility.IconContent("Prefab Icon").image, ScaleMode.ScaleToFit);
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
