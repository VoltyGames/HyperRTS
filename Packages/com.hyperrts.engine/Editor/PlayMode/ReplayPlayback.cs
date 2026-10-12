using HyperRTS.Simulation.Replays;
using Unity.Entities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>Saves the Play-mode recording, or opens a replay's scene and plays it back once Play mode starts.</summary>
    [InitializeOnLoad]
    internal static class ReplayPlayback
    {
        private const string Extension = "hrreplay";

        // SessionState survives the domain reload that entering Play mode may bring; a static would not.
        private const string PendingKey = "HyperRTS.ReplayPlayback.Pending";

        static ReplayPlayback()
        {
            EditorApplication.playModeStateChanged += BeginPending;
            DropPendingUnlessEnteringPlay();
        }

        public static bool CanSave => TryGetRecordingWorld(out _);

        public static bool CanPlay => !Application.isPlaying;

        public static void SaveRecording()
        {
            if (!TryGetRecordingWorld(out var world))
            {
                return;
            }

            var path = EditorUtility.SaveFilePanel("Save Replay", "", "Replay", Extension);
            if (!string.IsNullOrEmpty(path))
            {
                // The scene opened to play; SubScenes load after it.
                var scene = SceneManager.GetSceneAt(0).path;
                ReplaySerializer.Save(ReplayRecorder.Snapshot(world, scene), path);
                Debug.Log($"Replay saved to {path}");
            }
        }

        public static void PlayReplay()
        {
            var path = EditorUtility.OpenFilePanel("Play Replay", "", Extension);
            if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            var scene = ReplaySerializer.Load(path).ScenePath;
            if (!string.IsNullOrEmpty(scene))
            {
                EditorSceneManager.OpenScene(scene);
            }

            SessionState.SetString(PendingKey, path);
            EditorApplication.EnterPlaymode();
            EditorApplication.update += DropPendingUnlessEnteringPlay;
        }

        private static void BeginPending(PlayModeStateChange change)
        {
            var path = SessionState.GetString(PendingKey, "");
            if (change != PlayModeStateChange.EnteredPlayMode || path.Length == 0)
            {
                return;
            }

            SessionState.EraseString(PendingKey);
            if (PlayWorld.TryGetAuthoritative(out var entityManager))
            {
                ReplayViewer.Begin(entityManager.World, ReplaySerializer.Load(path));
            }
        }

        // Entering Play can be refused (compile errors); drop the replay then so a later Play doesn't start it.
        private static void DropPendingUnlessEnteringPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= DropPendingUnlessEnteringPlay;
            SessionState.EraseString(PendingKey);
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
