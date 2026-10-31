using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
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
