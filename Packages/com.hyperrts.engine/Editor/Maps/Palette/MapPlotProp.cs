using System;
using HyperRTS.Editor.Maps.Library;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Palette
{
    /// <summary>An optional prop of a settlement plot (a well, woodpile, haystack, greenhouse) and where it goes.</summary>
    [Serializable]
    public sealed class MapPlotProp
    {
        [Tooltip("Palette key.")]
        [MapKey(typeof(MapProp))]
        public string kind;

        [Tooltip("Chance a plot has one.")]
        [Range(0f, 1f)]
        public float chance = 0.5f;

        public MapPlotZone zone = MapPlotZone.Yard;
    }
}
