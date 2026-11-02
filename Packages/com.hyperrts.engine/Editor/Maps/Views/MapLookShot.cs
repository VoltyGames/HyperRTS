using System;

namespace HyperRTS.Editor.Maps.Views
{
    /// <summary>One look capture image: its weather, look point, zoom height and file.</summary>
    [Serializable]
    internal sealed class MapLookShot
    {
        public string weather;
        public string point;
        public float zoom;
        public string path;
    }
}
