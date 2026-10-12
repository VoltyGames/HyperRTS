using System.Collections.Generic;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Report;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>Builds maps from editor UI: asks to save open scenes first and puts the scenes back afterwards.</summary>
    public static class MapBuildCommands
    {
        /// <summary>Builds one spec; does nothing if the user cancels saving the open scenes.</summary>
        public static MapReport Build(string spec)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return null;
            }

            var scene = EditorSceneManager.GetActiveScene().path;
            var result = MapBuilder.Build(spec);
            Reopen(scene);
            return result;
        }

        /// <summary>Builds every spec in the project with a progress bar.</summary>
        public static List<MapReport> BuildAll()
        {
            var results = new List<MapReport>();
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return results;
            }

            var scene = EditorSceneManager.GetActiveScene().path;
            var specs = MapPaths.All();
            try
            {
                for (var i = 0; i < specs.Length; i++)
                {
                    EditorUtility.DisplayProgressBar("HyperRTS", "Building " + MapPaths.NameOf(specs[i]), (float)i / specs.Length);
                    results.Add(MapBuilder.Build(specs[i]));
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Reopen(scene);
            }

            return results;
        }

        private static void Reopen(string scene)
        {
            if (!string.IsNullOrEmpty(scene) && EditorSceneManager.GetActiveScene().path != scene)
            {
                EditorSceneManager.OpenScene(scene);
            }
        }
    }
}
