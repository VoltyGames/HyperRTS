using System;
using UnityEngine;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>An optional prop of a settlement plot (a well, woodpile, haystack, greenhouse) and where it goes.</summary>
    [Serializable]
    public sealed class MapPlotProp
    {
        [Tooltip("Palette key.")]
        [MapKey(typeof(MapPaletteEntry))]
        public string kind;

        [Tooltip("Chance a plot has one.")]
        [Range(0f, 1f)]
        public float chance = 0.5f;

        public MapPlotZone zone = MapPlotZone.Yard;
    }
}
