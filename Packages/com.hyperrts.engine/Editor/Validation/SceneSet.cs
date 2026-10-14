using System.Collections.Generic;
using System.Linq;
using HyperRTS.Simulation.GameEntities;
using HyperRTS.Simulation.Match;
using Unity.Scenes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.Validation
{
    /// <summary>The loaded scenes and the HyperRTS objects in them, gathered once for every <see cref="ISceneRule"/>.</summary>
    public sealed class SceneSet
    {
        public SceneSet(IReadOnlyList<Scene> scenes)
        {
            Scenes = scenes;
            Roots = scenes.SelectMany(scene => scene.GetRootGameObjects()).ToList();
            Matches = Roots.SelectMany(root => root.GetComponentsInChildren<MatchAuthoring>(true)).ToList();
            Entities = Roots.SelectMany(root => root.GetComponentsInChildren<GameEntityAuthoring>(true)).ToList();
        }

        public IReadOnlyList<Scene> Scenes { get; }

        public IReadOnlyList<GameObject> Roots { get; }

        public IReadOnlyList<MatchAuthoring> Matches { get; }

        public IReadOnlyList<GameEntityAuthoring> Entities { get; }

        public bool AnySubScene => Scenes.Any(scene => scene.isSubScene);

        /// <summary>True when a loaded scene holds a <see cref="SubScene"/>, so it can host a match.</summary>
        public bool HostsSubScene => Roots.Any(root => root.GetComponentInChildren<SubScene>(true) != null);

        public static SceneSet Loaded()
        {
            var scenes = new List<Scene>();
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    scenes.Add(scene);
                }
            }

            return new SceneSet(scenes);
        }
    }
}
