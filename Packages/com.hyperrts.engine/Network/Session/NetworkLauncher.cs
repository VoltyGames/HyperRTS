using System;
using HyperRTS.Network.Players;
using UnityEngine;

namespace HyperRTS.Network.Session
{
    /// <summary>
    /// Starts a session from the command line (<see cref="LaunchOptions"/>): <c>-server</c>, <c>-host</c> or
    /// <c>-connect &lt;address&gt;</c> [<c>-observe</c>], with optional <c>-port</c> and <c>-scene</c>. A server may add
    /// <c>-exit-on-end</c>, <c>-time-limit &lt;minutes&gt;</c> and <c>-report &lt;path&gt;</c>. Player builds only.
    /// </summary>
    public static class NetworkLauncher
    {
        /// <summary>Exit code of a command-line player that could not start or report its session.</summary>
        public const int ErrorCode = 1;

        /// <summary>
        /// The scene a command-line session loads when it has no <c>-scene</c>; null reloads the first scene. A game
        /// sets it before the first scene loads, as when it reads the match from its own flag.
        /// </summary>
        public static string DefaultScene { get; set; }

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
            var path = options.ScenePath ?? DefaultScene;
            var scene = path != null ? SessionScene.Load(path) : SessionScene.ReloadActive;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => DefaultScene = null;
    }
}
