using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>Opens the game's menu scene and enters Play mode, then hands a straight-to-the-map choice to the game.</summary>
    [InitializeOnLoad]
    internal static class PlayLauncher
    {
        // SessionState survives the domain reload that entering Play mode may bring; a static would not.
        private const string PendingKey = "HyperRTS.PlayLauncher.Pending";

        static PlayLauncher()
        {
            EditorApplication.playModeStateChanged += BeginPending;
            DropPendingUnlessEnteringPlay();
        }

        public static void Play(LaunchGame game, LaunchChoice choice)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(game.MenuScene);
            if (choice.start == LaunchStart.Map)
            {
                SessionState.SetString(PendingKey, JsonUtility.ToJson(choice));
            }

            EditorApplication.EnterPlaymode();
            EditorApplication.update += DropPendingUnlessEnteringPlay;
        }

        private static void BeginPending(PlayModeStateChange change)
        {
            var json = SessionState.GetString(PendingKey, "");
            if (change != PlayModeStateChange.EnteredPlayMode || json.Length == 0)
            {
                return;
            }

            SessionState.EraseString(PendingKey);
            LaunchGame.Find().Start(JsonUtility.FromJson<LaunchChoice>(json));
        }

        // Entering Play can be refused (compile errors); drop the choice then so a later Play doesn't start it.
        private static void DropPendingUnlessEnteringPlay()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            EditorApplication.update -= DropPendingUnlessEnteringPlay;
            SessionState.EraseString(PendingKey);
        }
    }
}
