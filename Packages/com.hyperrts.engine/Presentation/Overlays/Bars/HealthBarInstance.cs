using UnityEngine;

namespace HyperRTS.Presentation.Overlays.Bars
{
    /// <summary>One health bar as <c>HyperRTS/Health Bar</c> reads it.</summary>
    public struct HealthBarInstance
    {
        /// <summary>Health fraction, segments, rank and opacity.</summary>
        public Vector4 Bar;

        /// <summary>Width and bar height in reference pixels, squad members and members alive.</summary>
        public Vector4 Size;

        /// <summary>Suppression (-1 for a non-squad), its level, cover kind and 1 while retreating.</summary>
        public Vector4 Squad;

        /// <summary>World point the bar sits above.</summary>
        public Vector3 Anchor;
    }
}
