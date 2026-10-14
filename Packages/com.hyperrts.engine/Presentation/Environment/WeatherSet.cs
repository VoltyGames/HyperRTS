using System;
using System.Collections.Generic;
using HyperRTS.Core;
using HyperRTS.Simulation.Match;
using UnityEngine;

namespace HyperRTS.Presentation.Environment
{
    /// <summary>
    /// The weathers a map offers. Its only child is the default weather's atmosphere instance, baked into the scene;
    /// when play starts, a <see cref="MatchSetup.Weather"/> naming another entry replaces that child with its prefab.
    /// </summary>
    [AddComponentMenu(HyperRTSMenu.Environment + "Weather Set")]
    [Icon(HyperRTSIcons.Environment)]
    [HelpURL(HyperRTSDocs.Presentation)]
    [DisallowMultipleComponent]
    public sealed class WeatherSet : MonoBehaviour
    {
        [Tooltip("Key of the weather the scene was built with; its atmosphere is this object's child.")]
        public string defaultKey;

        public List<WeatherEntry> entries = new();

        private string _currentKey;

        // Awake, before the first frame renders, so the default weather never shows when another was picked.
        private void Awake()
        {
            _currentKey = defaultKey;
            var requested = MatchSetupRequest.TryGet(out var setup) ? setup.Weather.ToString() : "";
            Apply(requested);
        }

        /// <summary>Switches to <paramref name="key"/>; empty or the current key keeps the weather in effect.</summary>
        public void Apply(string key)
        {
            if (string.IsNullOrEmpty(key) || key == _currentKey)
            {
                return;
            }

            var prefab = Find(key);
            RemoveCurrent();
            Instantiate(prefab, transform);
            _currentKey = key;
        }

        /// <summary>The atmosphere prefab of <paramref name="key"/>; an unknown key throws naming it.</summary>
        public GameObject Find(string key)
        {
            foreach (var entry in entries)
            {
                if (entry.key == key)
                {
                    return entry.atmosphere;
                }
            }

            var known = string.Join(", ", entries.ConvertAll(entry => entry.key));
            throw new InvalidOperationException(
                $"HyperRTS: weather '{key}' is not one of this map's weathers ({known}).");
        }

        // Deactivated first so the old atmosphere's OnDisable runs before the new one's OnEnable publishes.
        private void RemoveCurrent()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
