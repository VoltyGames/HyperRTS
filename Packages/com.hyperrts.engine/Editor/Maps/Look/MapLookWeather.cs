using System;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>The means of the view numbers over one weather's shots, as <c>MapMetrics</c> keeps them for a build.</summary>
    [Serializable]
    internal sealed class MapLookWeather
    {
        public string weather;
        public float viewLuminanceSpread;
        public float viewLocalContrast;
        public float viewChroma;
    }
}
