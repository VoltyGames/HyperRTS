using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Rendering;
using Unity.Scenes;
using Object = UnityEngine.Object;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>
    /// Streams the open scene's SubScenes into the editor world and ticks it until entities are loaded and drawing
    /// has settled, so a capture shows them even when the editor is unfocused.
    /// </summary>
    public static class MapSubScenes
    {
        /// <summary>Generous bound: streaming, spawning and render registration take a few dozen updates.</summary>
        private const int MaxUpdates = 600;

        /// <summary>Updates the drawn entity count must hold for before the map counts as settled.</summary>
        private const int SettledUpdates = 10;

        public static void Load()
        {
            var world = World.DefaultGameObjectInjectionWorld
                ?? throw new InvalidOperationException("HyperRTS: no editor world to stream the map's SubScene into.");
            var flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn;
            var scenes = Object.FindObjectsByType<SubScene>().ToDictionary(
                subScene => subScene.name,
                subScene => SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters { Flags = flags }));
            using var unregistered = world.EntityManager.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<MaterialMeshInfo>() },
                None = new[] { ComponentType.ChunkComponentReadOnly<EntitiesGraphicsChunkInfo>() },
            });
            using var drawn = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<MaterialMeshInfo>());
            var (lastCount, steady) = (-1, 0);
            for (var i = 0; i < MaxUpdates; i++)
            {
                world.Update();
                var count = drawn.CalculateEntityCount();
                steady = count == lastCount ? steady + 1 : 0;
                lastCount = count;
                if (Unloaded(world, scenes).Count == 0 && unregistered.IsEmpty && steady >= SettledUpdates)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"HyperRTS: after {MaxUpdates} editor world updates, {Stuck(world, scenes, unregistered)}.");
        }

        private static string Stuck(World world, Dictionary<string, Entity> scenes, EntityQuery unregistered)
        {
            var waiting = Unloaded(world, scenes);
            if (waiting.Count > 0)
            {
                return $"SubScenes {string.Join(", ", waiting)} didn't load";
            }

            return unregistered.IsEmpty ? "the drawn entities kept changing" : "entity rendering didn't register every mesh";
        }

        private static List<string> Unloaded(World world, Dictionary<string, Entity> scenes) =>
            scenes.Where(scene => !SceneSystem.IsSceneLoaded(world.Unmanaged, scene.Value)).Select(scene => scene.Key).ToList();
    }
}
