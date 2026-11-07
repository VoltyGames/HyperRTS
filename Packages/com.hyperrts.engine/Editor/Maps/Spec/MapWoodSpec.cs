using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A stand of trees from a climate woodland kit: canopy, a shrubby edge, clearings and forest floor.</summary>
    [Serializable]
    [MapFeature("wood", 30, 0.15f, 0.55f, 0.2f, Closed = true)]
    public sealed class MapWoodSpec : MapFeatureSpec, IMapLine
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapWoodlandKit))]
        public string kind;

        public List<MapPoint> Points => points;
    }
}
