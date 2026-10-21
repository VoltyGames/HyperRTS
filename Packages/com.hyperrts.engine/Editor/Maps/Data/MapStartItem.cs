using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>One piece of a side's start kit: a prop, metres right (x) and forward (y) of the spawn, degrees of yaw.</summary>
    [Serializable]
    public sealed class MapStartItem
    {
        [MapKey(typeof(MapProp))]
        public string key;
        public Vector2 offset;
        public float yaw;
    }
}
