using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A row along a polyline every <see cref="spacing"/> metres: a climate tree (a shelterbelt) or a palette prop
    /// (pylons, fences).
    /// </summary>
    [Serializable]
    public sealed class MapRowSpec
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapClimateTree), typeof(MapProp))]
        public string kind;
        public float spacing = 8f;
        public bool shared;
    }
}
