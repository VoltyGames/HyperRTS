using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Layout
{
    /// <summary>An authored road while <see cref="MapRoadRefiner"/> works on it: its class and its rounded centre line.</summary>
    public sealed class MapRefinedRoad
    {
        public MapRefinedRoad(MapRoadSpec spec, MapRoadClass roadClass, List<Vector2> points)
        {
            Spec = spec;
            Class = roadClass;
            Points = points;
        }

        public MapRoadSpec Spec { get; }

        public MapRoadClass Class { get; }

        public List<Vector2> Points { get; set; }
    }
}
