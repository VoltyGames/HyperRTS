using System.IO;
using UnityEditor;
using UnityEngine;
using R = HyperRTS.Editor.Templates.TemplateRegistry;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// Assets ▸ Create ▸ HyperRTS ▸ Prefabs: every <see cref="TemplateRegistry"/> role as a prefab in the selected
    /// folder, named inline like any new asset.
    /// </summary>
    internal static class PrefabTemplateMenu
    {
        public const string Menu = "Assets/Create/HyperRTS/Prefabs/";

        [MenuItem(Menu + R.Unit, false, 0)]
        private static void Unit() => Create(R.Unit);

        [MenuItem(Menu + R.CombatUnit, false, 1)]
        private static void CombatUnit() => Create(R.CombatUnit);

        [MenuItem(Menu + R.Worker, false, 2)]
        private static void Worker() => Create(R.Worker);

        [MenuItem(Menu + R.Harvester, false, 3)]
        private static void Harvester() => Create(R.Harvester);

        [MenuItem(Menu + R.Transport, false, 4)]
        private static void Transport() => Create(R.Transport);

        [MenuItem(Menu + R.Aircraft, false, 5)]
        private static void Aircraft() => Create(R.Aircraft);

        [MenuItem(Menu + R.Building, false, 20)]
        private static void Building() => Create(R.Building);

        [MenuItem(Menu + R.Producer, false, 21)]
        private static void Producer() => Create(R.Producer);

        [MenuItem(Menu + R.ResearchBuilding, false, 22)]
        private static void Lab() => Create(R.ResearchBuilding);

        [MenuItem(Menu + R.DropOff, false, 23)]
        private static void DropOff() => Create(R.DropOff);

        [MenuItem(Menu + R.DefenseTower, false, 24)]
        private static void Tower() => Create(R.DefenseTower);

        [MenuItem(Menu + R.Airfield, false, 25)]
        private static void Airfield() => Create(R.Airfield);

        [MenuItem(Menu + R.CapturableBuilding, false, 26)]
        private static void Capturable() => Create(R.CapturableBuilding);

        [MenuItem(Menu + R.Upgrade, false, 40)]
        private static void Upgrade() => Create(R.Upgrade);

        [MenuItem(Menu + R.ResourceNode, false, 60)]
        private static void ResourceNode() => Create(R.ResourceNode);

        [MenuItem(Menu + R.NavObstacle, false, 61)]
        private static void NavObstacle() => Create(R.NavObstacle);

        [MenuItem(Menu + R.NavArea, false, 62)]
        private static void NavArea() => Create(R.NavArea);

        // The prefab is written once the name is confirmed in the Project window.
        private static void Create(string path)
        {
            var template = R.Get(path);
            var file = $"{SelectedFolder()}/{template.Label}.prefab";
            var icon = EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D;
            var save = ScriptableObject.CreateInstance<SaveTemplatePrefab>();
            save.Template = template;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(EntityId.None, save, file, icon, null);
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
