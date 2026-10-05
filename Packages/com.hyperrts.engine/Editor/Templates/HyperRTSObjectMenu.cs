using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Templates
{
    /// <summary>GameObject ▸ HyperRTS menu: role templates placed in the scene, and the engine rig.</summary>
    internal static class HyperRTSObjectMenu
    {
        private const string Menu = "GameObject/HyperRTS/";

        [MenuItem(Menu + "RTS World (Camera + HUD)", false, 10)]
        private static void CreateRTSWorld(MenuCommand cmd)
        {
            var rig = EditorAssets.RigPrefab;
            if (rig == null)
            {
                Debug.LogWarning("HyperRTS: the RTSWorld rig prefab is missing.");
                return;
            }

            Finish((GameObject)PrefabUtility.InstantiatePrefab(rig), cmd);
        }

        [MenuItem(Menu + "Match", false, 30)]
        private static void CreateMatch(MenuCommand cmd) => Finish(new GameObject("Match", typeof(MatchAuthoring)), cmd);

        [MenuItem(Menu + "Units/Unit", false, 40)]
        private static void CreateUnit(MenuCommand cmd) => Finish(EntityTemplates.Unit(), cmd);

        [MenuItem(Menu + "Units/Combat Unit", false, 41)]
        private static void CreateCombatUnit(MenuCommand cmd) => Finish(EntityTemplates.CombatUnit(), cmd);

        [MenuItem(Menu + "Units/Worker", false, 42)]
        private static void CreateWorker(MenuCommand cmd) => Finish(EntityTemplates.Worker(), cmd);

        [MenuItem(Menu + "Units/Harvester", false, 43)]
        private static void CreateHarvester(MenuCommand cmd) => Finish(EntityTemplates.Harvester(), cmd);

        [MenuItem(Menu + "Buildings/Building", false, 50)]
        private static void CreateBuilding(MenuCommand cmd) => Finish(EntityTemplates.Building(), cmd);

        [MenuItem(Menu + "Buildings/Producer", false, 51)]
        private static void CreateProducer(MenuCommand cmd) => Finish(EntityTemplates.Producer(), cmd);

        [MenuItem(Menu + "Buildings/Resource Drop-Off", false, 52)]
        private static void CreateDropOff(MenuCommand cmd) => Finish(EntityTemplates.DropOff(), cmd);

        [MenuItem(Menu + "Buildings/Defense Tower", false, 53)]
        private static void CreateTower(MenuCommand cmd) => Finish(EntityTemplates.DefenseTower(), cmd);

        [MenuItem(Menu + "Map/Resource Node", false, 60)]
        private static void CreateResourceNode(MenuCommand cmd) => Finish(EntityTemplates.ResourceNode(), cmd);

        [MenuItem(Menu + "Map/Nav Obstacle", false, 61)]
        private static void CreateObstacle(MenuCommand cmd) => Finish(EntityTemplates.NavObstacle(), cmd);

        private static void Finish(GameObject go, MenuCommand cmd)
        {
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            UnityEditor.Selection.activeGameObject = go;
        }
    }
}
