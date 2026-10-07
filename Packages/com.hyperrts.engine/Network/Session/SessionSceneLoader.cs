using UnityEngine;
using UnityEngine.SceneManagement;

namespace HyperRTS.Network.Session
{
    /// <summary>Loads the scene a session start asked for, so its SubScene streams into the session's worlds.</summary>
    internal static class SessionSceneLoader
    {
        public static AsyncOperation Current { get; private set; }

        public static void Load(SessionScene scene)
        {
            switch (scene.Mode)
            {
                case SessionScene.LoadMode.Keep:
                    return;
                case SessionScene.LoadMode.ReloadActive:
                    Current = LoadAsync(SceneManager.GetActiveScene().path);
                    return;
                default:
                    Current = LoadAsync(scene.Path);
                    return;
            }
        }

        private static AsyncOperation LoadAsync(string path)
        {
#if UNITY_EDITOR
            // Play mode can load any project scene, not only those in the build.
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(path,
                new LoadSceneParameters(LoadSceneMode.Single));
#else
            return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
#endif
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Current = null;
    }
}
