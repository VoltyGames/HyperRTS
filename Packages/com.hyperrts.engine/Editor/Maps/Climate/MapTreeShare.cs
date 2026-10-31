using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
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
