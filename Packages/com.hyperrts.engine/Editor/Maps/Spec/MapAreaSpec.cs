using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A polygon of one climate surface (field, forest, meadow): ground texture, trees and grass.</summary>
    [Serializable]
    public sealed class MapAreaSpec
    {
        public List<MapPoint> points = new();
        [MapKey(typeof(MapClimateSurface))]
        public string kind;
        public bool shared;
    }
}
