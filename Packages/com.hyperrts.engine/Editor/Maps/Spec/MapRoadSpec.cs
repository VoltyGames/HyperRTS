using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A road polyline painted with a climate surface and levelled across its width.</summary>
    [Serializable]
    public sealed class MapRoadSpec
    {
        public List<MapPoint> points = new();
        public float width = 6f;
        public string kind;
        public bool shared;
    }
}
