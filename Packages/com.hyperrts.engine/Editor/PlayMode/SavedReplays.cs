using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HyperRTS.Simulation.Replays;
using Unity.Entities;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Saves the Play-mode recording into Logs/Replays, or opens a replay's scene and plays it back once Play mode starts.</summary>
    [InitializeOnLoad]
    internal static class SavedReplays
    {
        private const string Extension = "hrreplay";
        private const string Folder = "Logs/Replays";
        private const int Recent = 8;

        [NoAutoStaticsCleanup]
        private static readonly PendingPlay Pending;

        static SavedReplays() => Pending = new PendingPlay("HyperRTS.SavedReplays.Pending", Begin);

        public static bool CanSave => TryGetRecordingWorld(out _);

        public static bool CanPlay => !Application.isPlaying;

        /// <summary>When a recording was last added to or removed from the folder.</summary>
        public static DateTime RecordingsChanged => Directory.Exists(Folder) ? Directory.GetLastWriteTimeUtc(Folder) : DateTime.MinValue;

        /// <summary>The newest recordings, newest first.</summary>
        public static List<string> RecentRecordings()
        {
            if (!Directory.Exists(Folder))
            {
                return new List<string>();
            }

            return Directory.GetFiles(Folder, "*." + Extension)
                .OrderByDescending(File.GetLastWriteTime)
                .Take(Recent)
                .Select(path => path.Replace("\\", "/"))
                .ToList();
        }

        public static void SaveRecording()
        {
            if (!TryGetRecordingWorld(out var world))
            {
                return;
            }

            // The scene opened to play; SubScenes load after it.
            var scene = SceneManager.GetSceneAt(0).path;
            Directory.CreateDirectory(Folder);
            var path = $"{Folder}/{Path.GetFileNameWithoutExtension(scene)}-{DateTime.Now:yyyyMMdd-HHmmss}.{Extension}";
            ReplaySerializer.Save(ReplayRecorder.Snapshot(world, scene), path);
            Debug.Log($"Replay saved to {path}");
        }

        public static void PlayReplay(string path)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = ReplaySerializer.Load(path).ScenePath;
            if (!string.IsNullOrEmpty(scene))
            {
                EditorSceneManager.OpenScene(scene);
            }

            Pending.Enter(path);
        }

        private static void Begin(string path)
        {
            if (PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                ReplayViewer.Begin(entityManager.World, ReplaySerializer.Load(path));
            }
        }

        private static bool TryGetRecordingWorld(out World world)
        {
            world = PlayWorld.Running.FirstOrDefault(ReplayRecorder.IsRecording);
            return world != null;
        }
    }
}
