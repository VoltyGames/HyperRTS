using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A gully cut along a polyline; its walls are too steep to drive, so it splits lanes.</summary>
    [Serializable]
    public sealed class MapRavineSpec
    {
        public List<MapPoint> points = new();
        public float width = 24f;
        public float depth = 6f;
        public bool shared;
    }
}
