using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// A map's scene and SubScene open for a build, cleared of what the last build generated and with the match set
    /// up. Generated content lives under the main scene's <c>[Generated]</c> root and in the SubScene roots
    /// <see cref="MapOwnedObjects"/> names; <c>[Dressing]</c> is never touched.
    /// </summary>
    public sealed class MapScene
    {
        public const string Generated = "[Generated]";
        public const string Dressing = "[Dressing]";

        private MapScene(Scene main, Scene entities, Transform root)
        {
            Main = main;
            Entities = entities;
            Root = root;
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
            Clear(main, entities);
            var root = new GameObject(Generated).transform;
            SceneManager.MoveGameObjectToScene(root.gameObject, main);
            EnsureDressing(main);
            EnsureDressing(entities);
            ConfigureMatch(entities, generation);
            return new MapScene(main, entities, root);
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

        /// <summary>Names a generated SubScene object so the next build clears it.</summary>
        public static void NameGenerated(GameObject entity, string name) => entity.name = $"{Generated} {name}";

        public void SaveMain() => EditorSceneManager.SaveScene(Main);

        /// <summary>Saves both scenes and closes the SubScene, leaving the map's scene open.</summary>
        public void SaveAndCloseEntities()
        {
            EditorSceneManager.SaveScene(Entities);
            EditorSceneManager.SaveScene(Main);
            EditorSceneManager.CloseScene(Entities, true);
        }

        private static void Clear(Scene main, Scene entities)
        {
            foreach (var root in main.GetRootGameObjects())
            {
                if (root.name.StartsWith(Generated, System.StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(root);
                }
            }

            var libraryPrefabs = MapOwnedObjects.LibraryPrefabs(MapLibrary.Load());
            foreach (var root in entities.GetRootGameObjects())
            {
                if (MapOwnedObjects.IsOwned(root, libraryPrefabs))
                {
                    Object.DestroyImmediate(root);
                }
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
            match.maxSlope = project.Rules.maxSlope;
            match.waterLevel = layout.WaterHeight;
            match.floodBelowWaterLevel = true;
            match.players = layout.Spawns.Select(spawn => new PlayerSlot
            {
                name = spawn.Slot == 1 ? "Player" : $"AI {spawn.Slot - 1}",
                team = spawn.Team,
                color = PlayerSlot.Palette[(spawn.Slot - 1) % PlayerSlot.Palette.Length],
                control = spawn.Slot == 1 ? PlayerControl.LocalHuman : PlayerControl.AI,
                startingResources = new List<ResourceQuantity>(project.Palette.startingResources),
            }).ToList();
            EditorUtility.SetDirty(match);
        }
    }
}
