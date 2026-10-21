using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How much of a layer's ground one plant (grass, flowers) covers.</summary>
    [Serializable]
    public sealed class MapPlantCoverage
    {
        [MapKey(typeof(MapClimatePlant))]
        public string plant;

        [Range(0f, 1f)]
        public float coverage = 0.5f;
    }
}
