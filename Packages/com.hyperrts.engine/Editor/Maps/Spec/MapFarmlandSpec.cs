using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// Cultivated land split into fields on a grid running at <see cref="angle"/>, with crops, margins, belts and
    /// field tracks from a climate farmland kit. No points: it fills all land nothing else uses.
    /// </summary>
    [Serializable]
    [MapFeature("farmland", 10, 0.85f, 0.75f, 0.3f, Closed = true)]
    public sealed class MapFarmlandSpec : MapFeatureSpec, IMapLine
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapFarmlandKit))]
        public string kind;

        /// <summary>Direction the crop rows and the long sides of fields run, in degrees clockwise from north.</summary>
        public float angle;

        public List<MapPoint> Points => points;
    }
}
