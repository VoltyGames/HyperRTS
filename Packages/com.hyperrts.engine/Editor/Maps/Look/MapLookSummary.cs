using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Look
{
    /// <summary>What a capture run wrote, saved as summary.json beside the images.</summary>
    [Serializable]
    internal sealed class MapLookSummary
    {
        public string map;
        public string quality;

        /// <summary>False when the weathers' Color Lookup was off, for fitting lookups.</summary>
        public bool graded;
        public int width;
        public int height;
        public string failure;
        public List<MapLookShot> shots = new();
    }
}
