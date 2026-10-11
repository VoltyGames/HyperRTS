using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>One piece of the start kit.</summary>
    [Serializable]
    public sealed class MapStartItem
    {
        public string key;
        public Vector2 offset;
        public float yaw;
    }
}
