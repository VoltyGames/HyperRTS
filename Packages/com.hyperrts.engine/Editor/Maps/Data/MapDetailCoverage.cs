using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How much of a layer's ground one detail (grass, flowers) covers.</summary>
    [Serializable]
    public sealed class MapDetailCoverage
    {
        public string detail;

        [Range(0f, 1f)]
        public float coverage = 0.5f;
    }
}
