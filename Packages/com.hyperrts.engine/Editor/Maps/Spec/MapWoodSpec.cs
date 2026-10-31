using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A stand of trees from a climate woodland kit: canopy, a shrubby edge, clearings and forest floor.</summary>
    [Serializable]
    public sealed class MapWoodSpec
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapWoodlandKit))]
        public string kind;
        public bool shared;
    }
}
