using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Match;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using R = HyperRTS.Editor.Templates.TemplateRegistry;

namespace HyperRTS.Editor.Templates
{
    /// <summary>
    /// GameObject ▸ HyperRTS menu: the engine rig, a Match, and every <see cref="TemplateRegistry"/> role. New objects
    /// go into the SubScene open for editing, when there is one.
    /// </summary>
    internal static class HyperRTSObjectMenu
    {
        public const string Menu = "GameObject/HyperRTS/";

        [MenuItem(Menu + "RTS World (Camera + HUD)", false, 10)]
        private static void CreateRTSWorld(MenuCommand cmd)
        {
            var rig = EditorAssets.RigPrefab;
            if (rig == null)
            {
                Debug.LogError("HyperRTS: the RTSWorld rig prefab is missing from the engine package.");
                return;
            }

            Finish((GameObject)PrefabUtility.InstantiatePrefab(rig), cmd.context as GameObject);
        }

        [MenuItem(Menu + "Match", false, 30)]
        private static void CreateMatch(MenuCommand cmd)
        {
            var statePrefab = EditorAssets.MatchStatePrefab;
            if (statePrefab == null)
            {
                Debug.LogError("HyperRTS: the MatchState prefab is missing from the engine package.");
                return;
            }

            var match = Place(new GameObject("Match", typeof(MatchAuthoring)), cmd);
            var state = (GameObject)PrefabUtility.InstantiatePrefab(statePrefab, match.scene);
            Undo.RegisterCreatedObjectUndo(state, "Create " + state.name);
            UnityEditor.Selection.activeGameObject = match;
        }

        [MenuItem(Menu + R.Unit, false, 40)]
        private static void Unit(MenuCommand c) => Create(R.Unit, c);

        [MenuItem(Menu + R.CombatUnit, false, 41)]
        private static void CombatUnit(MenuCommand c) => Create(R.CombatUnit, c);

        [MenuItem(Menu + R.Worker, false, 42)]
        private static void Worker(MenuCommand c) => Create(R.Worker, c);

        [MenuItem(Menu + R.Harvester, false, 43)]
        private static void Harvester(MenuCommand c) => Create(R.Harvester, c);

        [MenuItem(Menu + R.Transport, false, 44)]
        private static void Transport(MenuCommand c) => Create(R.Transport, c);

        [MenuItem(Menu + R.Aircraft, false, 45)]
        private static void Aircraft(MenuCommand c) => Create(R.Aircraft, c);

        [MenuItem(Menu + R.Building, false, 50)]
        private static void Building(MenuCommand c) => Create(R.Building, c);

        [MenuItem(Menu + R.Producer, false, 51)]
        private static void Producer(MenuCommand c) => Create(R.Producer, c);

        [MenuItem(Menu + R.ResearchBuilding, false, 52)]
        private static void Lab(MenuCommand c) => Create(R.ResearchBuilding, c);

        [MenuItem(Menu + R.DropOff, false, 53)]
        private static void DropOff(MenuCommand c) => Create(R.DropOff, c);

        [MenuItem(Menu + R.DefenseTower, false, 54)]
        private static void Tower(MenuCommand c) => Create(R.DefenseTower, c);

        [MenuItem(Menu + R.Airfield, false, 55)]
        private static void Airfield(MenuCommand c) => Create(R.Airfield, c);

        [MenuItem(Menu + R.CapturableBuilding, false, 56)]
        private static void Capturable(MenuCommand c) => Create(R.CapturableBuilding, c);

        [MenuItem(Menu + R.Upgrade, false, 60)]
        private static void Upgrade(MenuCommand c) => Create(R.Upgrade, c);

        [MenuItem(Menu + R.ResourceNode, false, 70)]
        private static void ResourceNode(MenuCommand c) => Create(R.ResourceNode, c);

        [MenuItem(Menu + R.NavObstacle, false, 71)]
        private static void NavObstacle(MenuCommand c) => Create(R.NavObstacle, c);

        [MenuItem(Menu + R.NavArea, false, 72)]
        private static void NavArea(MenuCommand c) => Create(R.NavArea, c);

        private static void Create(string path, MenuCommand cmd) => Place(R.Get(path).Create(), cmd);

        // Under the right-clicked object, else into the SubScene being edited.
        private static GameObject Place(GameObject go, MenuCommand cmd)
        {
            var parent = cmd.context as GameObject;
            if (parent == null)
            {
                MoveToEditedSubScene(go);
            }

            Finish(go, parent);
            return go;
        }

        // Prefers the active scene when several SubScenes are open.
        private static void MoveToEditedSubScene(GameObject go)
        {
            var open = Object.FindObjectsByType<SubScene>(FindObjectsInactive.Exclude)
                .Where(subScene => subScene.IsLoaded)
                .Select(subScene => subScene.EditingScene)
                .ToList();
            if (open.Count == 0)
            {
                return;
            }

            var active = SceneManager.GetActiveScene();
            SceneManager.MoveGameObjectToScene(go, open.Contains(active) ? active : open[0]);
        }

        private static void Finish(GameObject go, GameObject parent)
        {
            GameObjectUtility.SetParentAndAlign(go, parent);
            Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
            UnityEditor.Selection.activeGameObject = go;
        }
    }
}
