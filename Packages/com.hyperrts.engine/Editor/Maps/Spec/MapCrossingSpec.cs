using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A bridge or ford at <see cref="along"/> (0 to 1 along the river polyline).</summary>
    [Serializable]
    public sealed class MapCrossingSpec
    {
        public const string Bridge = "bridge";
        public const string Ford = "ford";

        public float along;
        public string kind;
        public float width = 10f;

        /// <summary>Bridges: the palette key of the bridge prefab.</summary>
        [MapKey(typeof(MapProp))]
        public string prop;
    }
}
