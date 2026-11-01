using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A polygon of one climate surface (field, forest, meadow): ground texture, trees and grass.</summary>
    [Serializable]
    [MapFeature("area", 20, 0.55f, 0.8f, 0.4f, Closed = true)]
    public sealed class MapAreaSpec : MapFeatureSpec, IMapLine
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapClimateSurface))]
        public string kind;

        public List<MapPoint> Points => points;
    }
}
