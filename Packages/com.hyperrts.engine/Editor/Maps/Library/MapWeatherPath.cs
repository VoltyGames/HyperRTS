using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>A climate weather as its library file writes it: the key and the atmosphere prefab's asset path.</summary>
    [Serializable]
    public sealed class MapWeatherPath
    {
        public string key;
        [MapAsset(typeof(GameObject))]
        public string atmosphere;
    }
}
