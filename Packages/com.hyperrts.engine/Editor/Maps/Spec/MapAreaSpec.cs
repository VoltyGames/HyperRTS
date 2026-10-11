using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A polygon of one climate surface (field, forest, meadow): ground texture, trees and grass.</summary>
    [Serializable]
    public sealed class MapAreaSpec
    {
        public List<MapPoint> points = new();
        public string kind;
        public bool shared;
    }
}
