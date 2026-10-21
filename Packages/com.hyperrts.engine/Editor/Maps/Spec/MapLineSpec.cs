using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A row along a polyline every <see cref="spacing"/> metres: a climate tree (a shelterbelt) or a palette prop
    /// (pylons, fences).
    /// </summary>
    [Serializable]
    public sealed class MapLineSpec
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapClimateTree), typeof(MapProp))]
        public string kind;
        public float spacing = 8f;
        public bool shared;
    }
}
