using System;
using HyperRTS.Network.Joining;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Starts a session from the command line (<see cref="LaunchOptions"/>): <c>-server</c>, <c>-host</c> or
    /// <c>-connect &lt;address&gt;</c>. Player builds only.
    /// </summary>
    public static partial class NetworkLauncher
    {
        /// <summary>Exit code of a command-line player that could not start or report its session.</summary>
        public const int ErrorCode = 1;

        /// <summary>
        /// The scene, and the match setup, a command-line session starts with; the default reloads the first scene. A
        /// <c>-scene</c> replaces its path only. A game sets it before the first scene loads, as when it reads the match
        /// from its own flag.
        /// </summary>
        [AutoStaticsCleanup]
        public static SessionScene DefaultScene { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Application.isEditor || NetworkSession.IsRunning)
            {
                return;
            }

            LaunchOptions options;
            try
            {
                options = LaunchOptions.Parse(Environment.GetCommandLineArgs());
            }
            catch (ArgumentException exception)
            {
                Debug.LogError($"[HyperRTS] {exception.Message}");
                Application.Quit(ErrorCode);
                return;
            }

            if (options.Mode == LaunchMode.None)
            {
                return;
            }

            if (Application.isBatchMode)
            {
                CapFrameRate();
            }

            if (!Start(options))
            {
                Application.Quit(ErrorCode);
                return;
            }

            if (options.EndsItself)
            {
                ServerMonitor.Begin(options);
            }
        }

        private static bool Start(LaunchOptions options)
        {
            var scene = DefaultScene;
            if (options.ScenePath != null)
            {
                scene = SessionScene.Load(options.ScenePath).WithSetup(scene.Setup);
            }

            NetworkSession.Preference = new JoinPreference { Observe = options.Observe };
            return options.Mode switch
            {
                LaunchMode.Server => NetworkSession.StartServer(options.Port, scene),
                LaunchMode.Host => NetworkSession.StartHost(options.Port, scene),
                _ => NetworkSession.StartClient(options.Address, options.Port, scene),
            };
        }

        // Nothing renders in batch mode, so an uncapped loop would only spin: one frame per simulation tick.
        private static void CapFrameRate()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = SessionWorlds.ServerTickRate().SimulationTickRate;
        }
    }
}
