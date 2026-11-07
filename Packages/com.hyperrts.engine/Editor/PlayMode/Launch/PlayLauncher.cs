using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.Launch
{
    /// <summary>Opens the game's menu scene and enters Play mode, then hands a straight-to-the-map choice to the game.</summary>
    [InitializeOnLoad]
    internal static class PlayLauncher
    {
        [NoAutoStaticsCleanup]
        private static readonly PendingPlay Pending;

        static PlayLauncher() => Pending = new PendingPlay("HyperRTS.PlayLauncher.Pending", Begin);

        public static void Play(LaunchGame game, LaunchChoice choice)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            EditorSceneManager.OpenScene(game.MenuScene);
            Pending.Enter(choice.start == LaunchStart.Map ? JsonUtility.ToJson(choice) : "");
        }

        private static void Begin(string json) => LaunchGame.Find().Start(JsonUtility.FromJson<LaunchChoice>(json));
    }
}
