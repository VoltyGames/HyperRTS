using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How many of one tree grow per hectare of a surface.</summary>
    [Serializable]
    public sealed class MapTreeDensity
    {
        [MapKey(typeof(MapClimateTree))]
        public string tree;
        public float perHectare = 100f;

        [Tooltip("Grow only within this many metres of the surface's edge, per hectare of that band (scrub along a margin); 0 grows anywhere. Surface trees only.")]
        public float maxEdgeDistance;
    }
}
