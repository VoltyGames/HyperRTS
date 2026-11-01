using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary><see cref="count"/> palette props dropped at random inside a polygon (hay bales, rocks, wrecks).</summary>
    [Serializable]
    [MapFeature("scatter", 40, 0.9f, 0.6f, 0.6f, Closed = true)]
    public sealed class MapScatterSpec : MapFeatureSpec, IMapLine
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapProp))]
        public string kind;
        public int count;

        public List<MapPoint> Points => points;
    }
}
