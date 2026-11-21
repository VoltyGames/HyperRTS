using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Editor.Maps.Build;
using HyperRTS.Editor.Maps.Report;
using HyperRTS.Editor.PlayMode;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>
    /// Plays a built map and renders each look point at three zooms in every weather into
    /// <c>Logs/MapReports/&lt;Map&gt;/look/&lt;Weather&gt;/</c>, then leaves Play mode. Its start arguments and status sit
    /// in SessionState, so entering Play, and the reload it may bring, loses nothing.
    /// </summary>
    [InitializeOnLoad]
    internal static partial class MapLookCapture
    {
        private const string PendingKey = "HyperRTS.MapLook.Pending";
        private const string ActiveKey = "HyperRTS.MapLook.Active";
        private const string StatusKey = "HyperRTS.MapLook.Status";

        [NoAutoStaticsCleanup]
        private static readonly PendingPlay Pending;

        [AutoStaticsCleanup]
        private static MapLookRun _run;

        static MapLookCapture()
        {
            Pending = new PendingPlay(PendingKey, Begin);
            EditorApplication.playModeStateChanged += EndIfPlayLeftEarly;
        }

        /// <summary>The arguments of <see cref="Start"/>, carried through SessionState to the Play that runs them.</summary>
        [Serializable]
        private struct Arguments
        {
            public string specPath;
            public string weathers;
            public bool graded;
            public string quality;
            public bool cost;
        }

        public static bool IsRunning => SessionState.GetBool(ActiveKey, false);

        /// <summary>Where the run is, or how the last one ended.</summary>
        public static string Status
        {
            get => SessionState.GetString(StatusKey, "");
            private set => SessionState.SetString(StatusKey, value);
        }

        /// <summary>The folder a map's look captures go to; ungraded ones, taken with the lookup off, apart.</summary>
        public static string Folder(string map, bool graded) =>
            Path.Combine(MapReport.ViewsFolder(map), graded ? "look" : "look-ungraded");

        /// <summary>
        /// Opens the map's scene and enters Play mode to start the captures. <paramref name="weathers"/> is a comma
        /// list of weather keys (empty for all), <paramref name="graded"/> false turns Color Lookup off, <paramref
        /// name="quality"/> names a quality level (empty for current), <paramref name="cost"/> measures frame times
        /// into <c>cost.json</c> instead.
        /// </summary>
        public static string Start(string specPath, string weathers, bool graded, string quality, bool cost)
        {
            if (IsRunning || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("HyperRTS: stop Play mode, or the running look capture, first.");
            }

            QualityLevel(quality);
            using var project = MapProject.Open(specPath);
            if (project.Spec.looks.Count == 0)
            {
                throw new InvalidOperationException($"HyperRTS: map '{project.Spec.name}' has no looks in its spec.");
            }

            project.OpenScene();

            var arguments = new Arguments { specPath = specPath, weathers = weathers, graded = graded, quality = quality, cost = cost };
            Status = $"Starting {project.Spec.name}";
            SessionState.SetBool(ActiveKey, true);
            // A frame after opening the scene, so its objects wake in Play instead of half-loaded.
            EditorApplication.delayCall += () =>
            {
                Pending.Enter(JsonUtility.ToJson(arguments));
                EditorApplication.update += WatchEntry;
            };
            return Folder(project.Spec.name, graded);
        }

        private static void Begin(string json)
        {
            try
            {
                var arguments = JsonUtility.FromJson<Arguments>(json);
                using var project = MapProject.Open(arguments.specPath);
                _run = new MapLookRun(project.Spec, project.Climate.water, Split(arguments.weathers), arguments.graded,
                    QualityLevel(arguments.quality), arguments.cost);
            }
            catch (Exception exception)
            {
                Status = $"Failed: {exception.Message}";
                SessionState.SetBool(ActiveKey, false);
                Debug.LogException(exception);
                EditorApplication.ExitPlaymode();
                return;
            }

            Status = $"Starting {_run.Summary.map}";
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StopOnExit;
        }

        // Play that never started (compile errors) leaves nothing else to end the run.
        private static void WatchEntry()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.update -= WatchEntry;
                }

                return;
            }

            EditorApplication.update -= WatchEntry;
            Status = "Failed: Play mode did not start; see the Console.";
            SessionState.SetBool(ActiveKey, false);
        }

        private static void EndIfPlayLeftEarly(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode && IsRunning)
            {
                Status = "Failed: Play ended before the captures began.";
                SessionState.SetBool(ActiveKey, false);
            }
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
            SessionState.SetBool(ActiveKey, false);
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
