using Unity.Entities;
using Unity.Scenes;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>
    /// Streams the open scene's SubScenes into the editor world and waits for them, so a capture shows the map's
    /// entities (props, buildings, destructible trees) even while the editor sits unfocused and isn't ticking.
    /// </summary>
    public static class MapSubScenes
    {
        /// <summary>World updates after loading, for entity rendering to pick the new entities up.</summary>
        private const int Settle = 3;

        public static void Load()
        {
            var world = World.DefaultGameObjectInjectionWorld
                ?? throw new System.InvalidOperationException("HyperRTS: no editor world to stream the map's SubScene into.");
            var flags = SceneLoadFlags.BlockOnImport | SceneLoadFlags.BlockOnStreamIn;
            foreach (var subScene in Object.FindObjectsByType<SubScene>(FindObjectsSortMode.None))
            {
                var scene = SceneSystem.LoadSceneAsync(world.Unmanaged, subScene.SceneGUID,
                    new SceneSystem.LoadParameters { Flags = flags });
                world.Update();
                if (!SceneSystem.IsSceneLoaded(world.Unmanaged, scene))
                {
                    throw new System.InvalidOperationException($"HyperRTS: SubScene '{subScene.name}' didn't load.");
                }
            }

            for (var i = 0; i < Settle; i++)
            {
                world.Update();
            }
        }
    }
}
