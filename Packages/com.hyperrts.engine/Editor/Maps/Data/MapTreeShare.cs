using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How often one climate tree is picked from a mix.</summary>
    [Serializable]
    public sealed class MapTreeShare
    {
        [MapKey(typeof(MapClimateTree))]
        public string tree;

        [Min(0f)]
        public float weight = 1f;
    }
}
