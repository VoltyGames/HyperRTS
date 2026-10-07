using UnityEngine;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Game speed and pause for single-player matches, through <see cref="Time.timeScale"/>. Networked matches run on
    /// the server's tick and must not use it. Camera and HUD run on unscaled time, so they keep working while paused.
    /// </summary>
    public static class LocalGameSpeed
    {
        private static float _scale = 1f;

        public static bool IsPaused { get; private set; }

        /// <summary>Speed while not paused.</summary>
        public static float Scale => _scale;

        public static void SetScale(float scale)
        {
            _scale = Mathf.Max(0f, scale);
            Apply();
        }

        public static void Pause()
        {
            IsPaused = true;
            Apply();
        }

        public static void Resume()
        {
            IsPaused = false;
            Apply();
        }

        /// <summary>Normal speed, unpaused: call when leaving a match.</summary>
        public static void Reset()
        {
            _scale = 1f;
            Resume();
        }

        private static void Apply() => Time.timeScale = IsPaused ? 0f : _scale;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _scale = 1f;
            IsPaused = false;
        }
    }
}
