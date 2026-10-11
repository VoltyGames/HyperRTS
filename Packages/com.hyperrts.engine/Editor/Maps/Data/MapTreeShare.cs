using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How often one climate tree is picked from a mix.</summary>
    [Serializable]
    public sealed class MapTreeShare
    {
        public string tree;

        [Min(0f)]
        public float weight = 1f;
    }
}
