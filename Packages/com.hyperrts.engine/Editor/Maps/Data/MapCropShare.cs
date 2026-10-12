using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How often fields of a farmland kit grow one crop surface.</summary>
    [Serializable]
    public sealed class MapCropShare
    {
        [MapKey(typeof(MapClimateSurface))]
        public string surface;

        [Min(0f)]
        public float weight = 1f;
    }
}
