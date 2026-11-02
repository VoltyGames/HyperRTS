using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>
    /// Plays a built map and renders each of its spec's look points at the closest, middle and farthest zoom in every
    /// weather, once the frame's GI has built up, into <c>Logs/MapReports/&lt;Map&gt;/look/&lt;Weather&gt;/</c>. Ends
    /// with <c>summary.json</c> there and leaves Play mode.
    /// </summary>
    internal static class MapLookCapture
    {
        [AutoStaticsCleanup]
        private static MapLookRun _run;

        public static bool IsRunning => _run != null;

        [AutoStaticsCleanup]
        public static string Status { get; private set; } = "";

        /// <summary>The folder a map's look captures go to.</summary>
        public static string Folder(string map) => Path.Combine(MapReport.ViewsFolder(map), "look");

        /// <summary>
        /// Opens the map's scene and starts the captures in Play mode. <paramref name="weathers"/> is a comma list of
        /// weather keys, empty for all; <paramref name="quality"/> a quality level name, empty for the current one.
        /// </summary>
        public static string Start(string specPath, string weathers, string quality)
        {
            if (IsRunning || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("HyperRTS: stop Play mode, or the running look capture, first.");
            }

            using var project = MapProject.Open(specPath);
            if (project.Spec.looks.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: map '{project.Spec.name}' has no looks in its spec.");
            }

            if (EditorSceneManager.GetActiveScene().path != project.Paths.Scene)
            {
                EditorSceneManager.OpenScene(project.Paths.Scene, OpenSceneMode.Single);
            }

            _run = new MapLookRun(project.Spec, Split(weathers), QualityLevel(quality));
            Status = $"Starting {_run.Summary.map}";
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StopOnExit;
            // A frame after opening the scene, so its objects wake in Play instead of half-loaded.
            EditorApplication.delayCall += EditorApplication.EnterPlaymode;
            return Folder(project.Spec.name);
        }

        private static List<string> Split(string list) =>
            list.Split(',').Select(key => key.Trim()).Where(key => key.Length > 0).ToList();

        private static int QualityLevel(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return QualitySettings.GetQualityLevel();
            }

            var index = Array.IndexOf(QualitySettings.names, name);
            return index >= 0
                ? index
                : throw new ArgumentException($"HyperRTS: no quality level '{name}'; levels: {string.Join(", ", QualitySettings.names)}.");
        }

        private static void Tick()
        {
            if (!EditorApplication.isPlaying)
            {
                return;
            }

            try
            {
                if (_run.Step())
                {
                    Finish(null);
                }
                else
                {
                    Status = _run.Progress;
                }
            }
            catch (Exception exception)
            {
                Finish(exception.Message);
                throw;
            }
        }

        private static void StopOnExit(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingPlayMode && IsRunning)
            {
                Finish("Play mode ended before the captures finished.");
            }
        }

        private static void Finish(string failure)
        {
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= StopOnExit;
            var run = _run;
            _run = null;
            var path = run.End(failure);
            Status = failure == null ? $"Done: {run.Summary.shots.Count} shots, {path}" : $"Failed: {failure} ({path})";
            Debug.Log($"HyperRTS: look capture of '{run.Summary.map}': {Status}");
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
