using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>
    /// A field's harvest swaths: stripes along its rows, one per pass of the combine, where straw lies and only a
    /// share of the plants stand.
    /// </summary>
    [Serializable]
    public struct RowCropSwaths
    {
        [Tooltip("Metres across the rows from one swath to the next; 0 for none.")]
        public float spacing;

        [Tooltip("Half a swath's width (metres).")]
        public float halfWidth;

        [Tooltip("Share of the plants still standing on a swath, 0 to 1.")]
        public float plants;

        [Tooltip("Metres added to a point's distance across the rows from the lattice origin before it is measured against the swaths.")]
        public float offset;

        public bool Any => spacing > 0f;

        /// <summary>Whether <paramref name="across"/>, metres across the rows from the lattice origin, lies on a swath.</summary>
        public bool Covers(float across)
        {
            if (!Any)
            {
                return false;
            }

            var fromCentre = Mathf.Abs(Mathf.Repeat(across + offset, spacing) - spacing * 0.5f);
            return fromCentre < halfWidth;
        }
    }
}
