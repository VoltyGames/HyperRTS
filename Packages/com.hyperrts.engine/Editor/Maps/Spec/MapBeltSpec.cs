using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A shelterbelt or hedgerow along a polyline: rows of trees and shrubs from a climate belt kit.</summary>
    [Serializable]
    public sealed class MapBeltSpec
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapBeltKit))]
        public string kind;
        public bool shared;
    }
}
