using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>A climate weather as its library file writes it: the key and the atmosphere prefab.</summary>
    [Serializable]
    public sealed class MapWeatherRef
    {
        public string key;
        [MapAsset(typeof(GameObject))]
        public MapAssetRef atmosphere = new();
    }
}
