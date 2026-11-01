using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A shelterbelt or hedgerow along a polyline: rows of trees and shrubs from a climate belt kit.</summary>
    [Serializable]
    [MapFeature("belt", 90, 0.35f, 0.7f, 0.3f)]
    public sealed class MapBeltSpec : MapFeatureSpec, IMapLine
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapBeltKit))]
        public string kind;

        public List<MapPoint> Points => points;
    }
}
