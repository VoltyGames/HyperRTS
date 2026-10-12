using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Maps.Scenes
{
    /// <summary>
    /// Opens a map's scene and SubScene (creating them the first time), clears what the last build generated and
    /// sets up the match. Generated content lives under the main scene's <c>[Generated]</c> root and in SubScene
    /// roots named <c>[Generated] …</c>; <c>[Dressing]</c> is never touched.
    /// </summary>
    public static class MapSceneWriter
    {
        public const string Generated = "[Generated]";
        public const string Dressing = "[Dressing]";

        /// <summary>The main scene (active) and the SubScene (open additively), both cleared of generated content.</summary>
        public static (Scene Main, Scene Entities, Transform Root) Open(MapPaths paths, MapLayout layout, MapProject project)
        {
            if (!File.Exists(paths.Scene))
            {
                RTSSceneBuilder.Build(new RTSSceneSpec
                {
                    ScenePath = paths.Scene,
                    MapSize = layout.Size,
                    Players = layout.Spawns.Count,
                    Rig = project.Palette.rig,
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
            ConfigureMatch(entities, layout, project);
            return (main, entities, root);
        }

        /// <summary>A SubScene object the build owns, at the scene root (entities must not have a parent).</summary>
        public static GameObject CreateEntityObject(Scene entities, string name)
        {
            var go = new GameObject($"{Generated} {name}");
            SceneManager.MoveGameObjectToScene(go, entities);
            return go;
        }

        public static void Rename(GameObject entity, string name) => entity.name = $"{Generated} {name}";

        private static void Clear(Scene main, Scene entities)
        {
            foreach (var root in main.GetRootGameObjects().Concat(entities.GetRootGameObjects())
                         .Where(r => r.name.StartsWith(Generated, System.StringComparison.Ordinal)))
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

        private static void ConfigureMatch(Scene entities, MapLayout layout, MapProject project)
        {
            var match = entities.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MatchAuthoring>(true))
                .Single();
            match.mapSize = layout.Size;
            match.navCellSize = project.Rules.navCellSize;
            match.maxSlope = project.Rules.maxSlope;
            match.waterLevel = layout.Terrain.waterLevel - layout.GroundLevel;
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
