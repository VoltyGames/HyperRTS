using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary><see cref="count"/> palette props dropped at random inside a polygon (hay bales, rocks, wrecks).</summary>
    [Serializable]
    public sealed class MapScatterSpec
    {
        public List<MapPoint> points = new();
        public string kind;
        public int count;
        public bool shared;
    }
}
