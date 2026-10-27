using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Simulation.Match;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// A map's scene and SubScene open for a build, cleared of what the last build generated and with the match set
    /// up. Generated content lives under the main scene's <c>[Generated]</c> root and in the SubScene roots
    /// <see cref="MapBuildObjects"/> owns. <c>[Dressing]</c> is never touched.
    /// </summary>
    public sealed class MapScene
    {
        public const string Generated = "[Generated]";
        public const string Dressing = "[Dressing]";

        private readonly MapBuildObjects _buildObjects;

        private MapScene(Scene main, Scene entities, Transform root, MapBuildObjects buildObjects)
        {
            Main = main;
            Entities = entities;
            Root = root;
            _buildObjects = buildObjects;
        }

        /// <summary>The scene with the terrain and scenery, active.</summary>
        public Scene Main { get; }

        /// <summary>The SubScene the gameplay entities bake from, open additively.</summary>
        public Scene Entities { get; }

        /// <summary>The main scene's generated root.</summary>
        public Transform Root { get; }

        /// <summary>Opens the map's scenes, creating them the first time.</summary>
        public static MapScene Open(MapGeneration generation)
        {
            var (paths, layout) = (generation.Project.Paths, generation.Layout);
            if (!File.Exists(paths.Scene))
            {
                RTSSceneBuilder.Build(new RTSSceneSpec
                {
                    ScenePath = paths.Scene,
                    MapSize = layout.Size,
                    Players = layout.Spawns.Count,
                    Rig = generation.Project.Palette.rig,
                    Environment = false,
                });
            }

            var main = EditorSceneManager.OpenScene(paths.Scene, OpenSceneMode.Single);
            var entities = EditorSceneManager.OpenScene(paths.SubScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(main);
            var buildObjects = new MapBuildObjects(paths.BuildObjects);
            Clear(main, entities, buildObjects);
            var root = new GameObject(Generated).transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, main);
            EnsureDressing(main);
            EnsureDressing(entities);
            ConfigureMatch(entities, generation);
            return new MapScene(main, entities, root, buildObjects);
        }

        /// <summary>A SubScene object the build owns, at the scene root (entities must not have a parent).</summary>
        public GameObject CreateEntityObject(string name)
        {
            var go = new GameObject($"{Generated} {name}");
            SceneManager.MoveGameObjectToScene(go, Entities);
            return go;
        }

        /// <summary>An empty object under the main scene's generated root.</summary>
        public Transform CreateChild(string name)
        {
            var child = new GameObject(name).transform;
            child.SetParent(Root, false);
            return child;
        }

        /// <summary>A SubScene instance of <paramref name="prefab"/> at the scene root, recorded so the next build clears it.</summary>
        public GameObject CreateEntityInstance(GameObject prefab, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, Entities);
            instance.name = $"{Generated} {name}";
            _buildObjects.Add(instance);
            return instance;
        }

        public void SaveMain() => EditorSceneManager.SaveScene(Main);

        /// <summary>Saves both scenes and closes the SubScene, leaving the map's scene open.</summary>
        public void SaveAndCloseEntities()
        {
            EditorSceneManager.SaveScene(Entities);
            _buildObjects.Save();
            EditorSceneManager.SaveScene(Main);
            EditorSceneManager.CloseScene(Entities, true);
        }

        private static void Clear(Scene main, Scene entities, MapBuildObjects buildObjects)
        {
            foreach (var root in main.GetRootGameObjects())
            {
                if (root.name.StartsWith(Generated, System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(root);
                }
            }

            foreach (var root in buildObjects.Owned(entities.GetRootGameObjects()))
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void EnsureDressing(Scene scene)
        {
            if (scene.GetRootGameObjects().All(r => r.name != Dressing))
            {
                SceneManager.MoveGameObjectToScene(new GameObject(Dressing), scene);
            }
        }

        private static void ConfigureMatch(Scene entities, MapGeneration generation)
        {
            var (layout, project) = (generation.Layout, generation.Project);
            var match = entities.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MatchAuthoring>(true))
                .Single();
            match.mapSize = layout.Size;
            match.navCellSize = project.Rules.navCellSize;
            match.buildingGap = project.Rules.buildingGap;
            match.maxSlope = project.Rules.maxSlope;
            match.waterLevel = layout.WaterHeight;
            match.floodBelowWaterLevel = true;
            var (heights, groundLevel) = (generation.Heights, layout.GroundLevel);
            match.players = MapMatchContent.Players(layout.Spawns, project.Palette,
                point => heights.Height(point) - groundLevel);
            match.sides = MapMatchContent.Sides(project.Palette);
            EditorUtility.SetDirty(match);
        }
    }
}
