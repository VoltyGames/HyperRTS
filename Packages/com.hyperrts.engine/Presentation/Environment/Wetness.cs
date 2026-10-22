using System;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>How wet the ground is after rain (0 dry, 1 soaked), published as <c>_HyperRTSWetness</c>.</summary>
    public static partial class Wetness
    {
        private static readonly int LevelId = Shader.PropertyToID("_HyperRTSWetness");

        [AutoStaticsCleanup]
        public static float Level { get; private set; }

        [AutoStaticsCleanup]
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

        // Shader globals outlive Play mode in the editor.
        [OnEnteringPlayMode]
        private static void ClearGlobal() => Shader.SetGlobalFloat(LevelId, 0f);
    }
}
