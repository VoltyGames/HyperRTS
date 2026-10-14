using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>How wet the ground is after rain (0 dry, 1 soaked), published as <c>_HyperRTSWetness</c>.</summary>
    public static class Wetness
    {
        private static readonly int LevelId = Shader.PropertyToID("_HyperRTSWetness");

        public static float Level { get; private set; }

        public static event Action Changed;

        public static void Publish(float level)
        {
            level = Mathf.Clamp01(level);
            Shader.SetGlobalFloat(LevelId, level);
            if (Mathf.Approximately(level, Level))
            {
                return;
            }

            Level = level;
            Changed?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
            Publish(0f);
        }
    }
}
