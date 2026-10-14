using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>A weather a map offers: its key and the atmosphere prefab that applies it.</summary>
    [Serializable]
    public struct WeatherEntry
    {
        public string key;
        public GameObject atmosphere;
    }
}
