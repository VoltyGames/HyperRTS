using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A shelterbelt or hedgerow along a polyline: rows of trees and shrubs from a climate belt kit.</summary>
    [Serializable]
    public sealed class MapBeltSpec
    {
        public List<MapPoint> points = new();
        public string kind;
        public bool shared;
    }
}
