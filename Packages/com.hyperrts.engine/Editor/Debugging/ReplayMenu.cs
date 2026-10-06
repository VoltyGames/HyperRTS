using HyperRTS.Simulation.Replays;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.Debugging
{
    /// <summary>HyperRTS ▸ Replays: save the Play-mode recording, or open a replay's scene and play it back.</summary>
    internal static class ReplayMenu
    {
        private const string Path = "HyperRTS/Replays/";
        private const string Extension = "hrreplay";

        private static Replay _pending;

        [MenuItem(Path + "Save Recording...", false, 50)]
        private static void Save()
        {
            if (!TryGetRecordingWorld(out var world))
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel("Save Replay", "", "Replay", Extension);
            if (!string.IsNullOrEmpty(path))
            {
                ReplaySerializer.Save(ReplayRecorder.Snapshot(world), path);
                Debug.Log($"Replay saved to {path}");
            }
        }

        [MenuItem(Path + "Save Recording...", true)]
        private static bool CanSave() => TryGetRecordingWorld(out _);

        [MenuItem(Path + "Play Replay...", false, 51)]
        private static void Play()
        {
            var path = EditorUtility.OpenFilePanel("Play Replay", "", Extension);
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            _pending = ReplaySerializer.Load(path);
            if (!string.IsNullOrEmpty(_pending.ScenePath))
            {
                EditorSceneManager.OpenScene(_pending.ScenePath);
            }

            EditorApplication.playModeStateChanged += BeginPending;
            EditorApplication.EnterPlaymode();
        }

        [MenuItem(Path + "Play Replay...", true)]
        private static bool CanPlay() => !Application.isPlaying;

        private static void BeginPending(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode)
            {
                return;
            }

            EditorApplication.playModeStateChanged -= BeginPending;
            ReplayViewer.Begin(World.DefaultGameObjectInjectionWorld, _pending);
            _pending = null;
        }

        private static bool TryGetRecordingWorld(out World world)
        {
            world = null;
            if (!Application.isPlaying)
            {
                return false;
            }

            foreach (var candidate in World.All)
            {
                if (ReplayRecorder.IsRecording(candidate))
                {
                    world = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}
