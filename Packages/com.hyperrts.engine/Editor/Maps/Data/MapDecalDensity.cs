using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How many of one decal a surface lays per hectare (puddles on tracks, leaf litter in woods).</summary>
    [Serializable]
    public sealed class MapDecalDensity
    {
        [MapKey(typeof(MapClimateDecal))]
        public string decal;

        [Min(0f)]
        public float perHectare = 1f;
    }
}
