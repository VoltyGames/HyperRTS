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
    /// Streams the open scene's SubScenes into the editor world and ticks it until they are loaded and entity
    /// rendering has registered every mesh, so a capture shows the map's entities even while the editor is unfocused.
    /// </summary>
    public static class MapSubScenes
    {
        /// <summary>Generous bound: streaming and render registration take a handful of updates.</summary>
        private const int MaxUpdates = 120;

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
            for (var i = 0; i < MaxUpdates; i++)
            {
                world.Update();
                if (Unloaded(world, scenes).Count == 0 && unregistered.IsEmpty)
                {
                    return;
                }
            }

            var waiting = Unloaded(world, scenes);
            var what = waiting.Count > 0
                ? $"SubScenes {string.Join(", ", waiting)} didn't load"
                : "entity rendering didn't register every mesh";
            throw new InvalidOperationException($"HyperRTS: after {MaxUpdates} editor world updates, {what}.");
        }

        private static List<string> Unloaded(World world, Dictionary<string, Entity> scenes) =>
            scenes.Where(scene => !SceneSystem.IsSceneLoaded(world.Unmanaged, scene.Value)).Select(scene => scene.Key).ToList();
    }
}
