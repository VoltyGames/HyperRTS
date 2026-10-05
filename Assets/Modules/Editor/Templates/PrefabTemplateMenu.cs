using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>Assets ▸ Create ▸ HyperRTS: saves a role template as a prefab in the selected folder.</summary>
    internal static class PrefabTemplateMenu
    {
        private const string Menu = "Assets/Create/HyperRTS/Prefabs/";

        [MenuItem(Menu + "Combat Unit", false, 0)]
        private static void CombatUnit() => Save(EntityTemplates.CombatUnit);

        [MenuItem(Menu + "Worker", false, 1)]
        private static void Worker() => Save(EntityTemplates.Worker);

        [MenuItem(Menu + "Harvester", false, 2)]
        private static void Harvester() => Save(EntityTemplates.Harvester);

        [MenuItem(Menu + "Producer Building", false, 20)]
        private static void Producer() => Save(EntityTemplates.Producer);

        [MenuItem(Menu + "Resource Drop-Off", false, 21)]
        private static void DropOff() => Save(EntityTemplates.DropOff);

        [MenuItem(Menu + "Defense Tower", false, 22)]
        private static void DefenseTower() => Save(EntityTemplates.DefenseTower);

        [MenuItem(Menu + "Resource Node", false, 40)]
        private static void ResourceNode() => Save(EntityTemplates.ResourceNode);

        private static void Save(Func<GameObject> build)
        {
            var go = build();
            var path = AssetDatabase.GenerateUniqueAssetPath($"{SelectedFolder()}/{go.name}.prefab");
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            ProjectWindowUtil.ShowCreatedAsset(prefab);
        }

        private static string SelectedFolder()
        {
            var path = AssetDatabase.GetAssetPath(UnityEditor.Selection.activeObject);
            if (string.IsNullOrEmpty(path))
            {
                return "Assets";
            }

            return AssetDatabase.IsValidFolder(path) ? path : Path.GetDirectoryName(path).Replace('\\', '/');
        }
    }
}
