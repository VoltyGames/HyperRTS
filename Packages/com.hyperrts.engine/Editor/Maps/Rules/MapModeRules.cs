using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Rules
{
    /// <summary>Distance and path thresholds for one mode (1v1, 2v2, 3v3, 4v4, ffa).</summary>
    [Serializable]
    public sealed class MapModeRules : IMapKeyed
    {
        public string mode;

        [Tooltip("Shortest allowed drive from a base to the nearest enemy base (seconds at vehicle speed).")]
        public float rushMin = 60f;

        [Tooltip("Longest allowed drive from a base to the nearest enemy base (seconds at vehicle speed).")]
        public float rushMax = 80f;

        [Tooltip("Fewest separate attack paths between opposing bases.")]
        [Min(1)]
        public int minPaths = 3;

        [Tooltip("Fewest renewable (regrowing) fields per player, counted over the whole map; stacks within 20 m of each other are one field.")]
        [Min(0)]
        public int minRenewableFields;

        string IMapKeyed.Key => mode;
    }
}
