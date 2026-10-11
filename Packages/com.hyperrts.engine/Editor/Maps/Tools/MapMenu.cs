using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>HyperRTS ▸ Maps: build the selected or open map, build them all, auto-rebuild, open the report.</summary>
    public static class MapMenu
    {
        private const string AutoRebuildKey = "HyperRTS.Maps.AutoRebuild";
        private const string Build = EditorMenu.Maps + "Build";
        private const string BuildAll = EditorMenu.Maps + "Build All";
        private const string AutoRebuild = EditorMenu.Maps + "Auto-rebuild";
        private const string OpenReport = EditorMenu.Maps + "Open Report";

        public static bool AutoRebuildOn
        {
            get => EditorPrefs.GetBool(AutoRebuildKey, false);
            private set => EditorPrefs.SetBool(AutoRebuildKey, value);
        }

        /// <summary>Every map spec in the project.</summary>
        public static string[] Specs() => AssetDatabase.FindAssets("t:TextAsset")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(MapPaths.SpecExtension, System.StringComparison.Ordinal))
            .ToArray();

        /// <summary>Builds after saving open scenes the user wants kept; does nothing if they cancel.</summary>
        public static void BuildSpec(string spec)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                MapBuilder.Build(spec);
            }
        }

        [MenuItem(Build, false, EditorMenu.MapsPriority)]
        private static void BuildCurrent()
        {
            var spec = CurrentSpec();
            if (spec == null)
            {
                Debug.LogWarning("HyperRTS: select a .map.json spec or open a map scene to build it.");
                return;
            }

            BuildSpec(spec);
        }

        [MenuItem(BuildAll, false, EditorMenu.MapsPriority + 1)]
        private static void BuildEvery()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            foreach (var spec in Specs())
            {
                MapBuilder.Build(spec);
            }
        }

        [MenuItem(AutoRebuild, false, EditorMenu.MapsPriority + 2)]
        private static void ToggleAutoRebuild() => AutoRebuildOn = !AutoRebuildOn;

        [MenuItem(AutoRebuild, true)]
        private static bool ShowAutoRebuild()
        {
            Menu.SetChecked(AutoRebuild, AutoRebuildOn);
            return true;
        }

        [MenuItem(OpenReport, false, EditorMenu.MapsPriority + 3)]
        private static void OpenCurrentReport()
        {
            var spec = CurrentSpec();
            var image = spec != null ? MapReport.ImagePath(MapPaths.NameOf(spec)) : null;
            if (image == null || !File.Exists(image))
            {
                Debug.LogWarning("HyperRTS: no report yet; build the map first.");
                return;
            }

            EditorUtility.OpenWithDefaultApp(MapReport.JsonPath(MapPaths.NameOf(spec)));
            EditorUtility.OpenWithDefaultApp(image);
        }

        /// <summary>The selected spec, else the spec next to the active scene.</summary>
        private static string CurrentSpec()
        {
            var selected = AssetDatabase.GetAssetPath(Selection.activeObject);
            if (selected.EndsWith(MapPaths.SpecExtension, System.StringComparison.Ordinal))
            {
                return selected;
            }

            var scene = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(scene))
            {
                return null;
            }

            var sibling = Path.ChangeExtension(scene, null) + MapPaths.SpecExtension;
            return File.Exists(sibling) ? sibling.Replace('\\', '/') : null;
        }
    }
}
