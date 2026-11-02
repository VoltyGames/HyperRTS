using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>What a capture run wrote, saved as summary.json beside the images.</summary>
    [Serializable]
    internal sealed class MapLookSummary
    {
        public string map;
        public string quality;
        public int width;
        public int height;
        public string failure;
        public List<MapLookShot> shots = new();
    }
}
