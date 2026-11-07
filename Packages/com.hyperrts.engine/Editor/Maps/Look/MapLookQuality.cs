using System;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>
    /// Lets a game apply the settings it ties to a quality level. Look captures and cost probes call it in Play after
    /// they set the Unity quality level, so what they report is what that level draws.
    /// </summary>
    public static class MapLookQuality
    {
        /// <summary>Called with the quality level's index each time a look run sets it.</summary>
        [NoAutoStaticsCleanup]
        public static Action<int> Applied;
    }
}
