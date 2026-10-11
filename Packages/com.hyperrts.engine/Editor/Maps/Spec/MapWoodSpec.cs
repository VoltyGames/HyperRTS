using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A stand of trees from a climate woodland kit: canopy, a shrubby edge, clearings and forest floor.</summary>
    [Serializable]
    public sealed class MapWoodSpec
    {
        public List<MapPoint> points = new();
        public string kind;
        public bool shared;
    }
}
