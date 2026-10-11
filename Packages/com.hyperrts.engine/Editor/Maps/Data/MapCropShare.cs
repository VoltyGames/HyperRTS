using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How often fields of a farmland kit grow one crop surface.</summary>
    [Serializable]
    public sealed class MapCropShare
    {
        public string surface;

        [Min(0f)]
        public float weight = 1f;
    }
}
