using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>One piece of the start kit.</summary>
    [Serializable]
    public sealed class MapStartItem
    {
        [MapKey(typeof(MapPaletteEntry))]
        public string key;
        public Vector2 offset;
        public float yaw;
    }
}
