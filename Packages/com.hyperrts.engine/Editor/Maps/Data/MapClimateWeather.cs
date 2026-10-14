using System;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>A weather a climate offers: the atmosphere prefab (sky, sun, fog, post) that applies it.</summary>
    [Serializable]
    public sealed class MapClimateWeather : IMapKeyed
    {
        public string key;
        public GameObject atmosphere;

        public string Key => key;
    }
}
