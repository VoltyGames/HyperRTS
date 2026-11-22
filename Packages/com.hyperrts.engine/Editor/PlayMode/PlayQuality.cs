using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode
{
    /// <summary>
    /// Sets a quality level in Play for look captures, cost probes and stagings, with the settings a game ties to that
    /// level, so what they show is what the level draws.
    /// </summary>
    public static class PlayQuality
    {
        /// <summary>Called with the quality level's index each time <see cref="Set"/> runs, to apply the game's settings for it.</summary>
        [NoAutoStaticsCleanup]
        public static Action<int> Applied;

        public static void Set(int level)
        {
            QualitySettings.SetQualityLevel(level, true);
            Applied?.Invoke(level);
        }
    }
}
