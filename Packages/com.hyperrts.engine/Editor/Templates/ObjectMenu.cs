using System.Linq;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Match;
using Unity.Scenes;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Templates;

/// <summary>
/// The create menu items for the engine rig, a Match and every <see cref="TemplateRegistry"/> role, placed in the
/// SubScene open for editing when there is one.
/// </summary>
internal static class ObjectMenu
{
    public const string Menu = "GameObject/HyperRTS/";

    [MenuItem(Menu + "Engine Rig", false, 10)]
    private static void CreateEngineRig(MenuCommand cmd)
    {
        var rig = EditorAssets.RigPrefab;
        if (rig == null)
        {
            Debug.LogError("HyperRTS: the EngineRig rig prefab is missing from the engine package.");
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

    [MenuItem(Menu + TemplateRegistry.Unit, false, 40)]
    private static void Unit(MenuCommand c) => Create(TemplateRegistry.Unit, c);

    [MenuItem(Menu + TemplateRegistry.CombatUnit, false, 41)]
    private static void CombatUnit(MenuCommand c) => Create(TemplateRegistry.CombatUnit, c);

    [MenuItem(Menu + TemplateRegistry.Worker, false, 42)]
    private static void Worker(MenuCommand c) => Create(TemplateRegistry.Worker, c);

    [MenuItem(Menu + TemplateRegistry.Harvester, false, 43)]
    private static void Harvester(MenuCommand c) => Create(TemplateRegistry.Harvester, c);

    [MenuItem(Menu + TemplateRegistry.Transport, false, 44)]
    private static void Transport(MenuCommand c) => Create(TemplateRegistry.Transport, c);

    [MenuItem(Menu + TemplateRegistry.Aircraft, false, 45)]
    private static void Aircraft(MenuCommand c) => Create(TemplateRegistry.Aircraft, c);

    [MenuItem(Menu + TemplateRegistry.Building, false, 50)]
    private static void Building(MenuCommand c) => Create(TemplateRegistry.Building, c);

    [MenuItem(Menu + TemplateRegistry.Producer, false, 51)]
    private static void Producer(MenuCommand c) => Create(TemplateRegistry.Producer, c);

    [MenuItem(Menu + TemplateRegistry.ResearchBuilding, false, 52)]
    private static void ResearchBuilding(MenuCommand c) => Create(TemplateRegistry.ResearchBuilding, c);

    [MenuItem(Menu + TemplateRegistry.DropOff, false, 53)]
    private static void DropOff(MenuCommand c) => Create(TemplateRegistry.DropOff, c);

    [MenuItem(Menu + TemplateRegistry.DefenseTower, false, 54)]
    private static void DefenseTower(MenuCommand c) => Create(TemplateRegistry.DefenseTower, c);

    [MenuItem(Menu + TemplateRegistry.Airfield, false, 55)]
    private static void Airfield(MenuCommand c) => Create(TemplateRegistry.Airfield, c);

    [MenuItem(Menu + TemplateRegistry.CapturableBuilding, false, 56)]
    private static void CapturableBuilding(MenuCommand c) => Create(TemplateRegistry.CapturableBuilding, c);

    [MenuItem(Menu + TemplateRegistry.Upgrade, false, 60)]
    private static void Upgrade(MenuCommand c) => Create(TemplateRegistry.Upgrade, c);

    [MenuItem(Menu + TemplateRegistry.ResourceNode, false, 70)]
    private static void ResourceNode(MenuCommand c) => Create(TemplateRegistry.ResourceNode, c);

    [MenuItem(Menu + TemplateRegistry.NavObstacle, false, 71)]
    private static void NavObstacle(MenuCommand c) => Create(TemplateRegistry.NavObstacle, c);

    [MenuItem(Menu + TemplateRegistry.NavArea, false, 72)]
    private static void NavArea(MenuCommand c) => Create(TemplateRegistry.NavArea, c);

    private static void Create(string path, MenuCommand cmd) => Place(TemplateRegistry.Get(path).Create(), cmd);

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
