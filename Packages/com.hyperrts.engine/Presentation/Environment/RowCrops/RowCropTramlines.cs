using System;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// A field's tramlines: pairs of unsown wheel lines along its rows, one pair every spacing metres.
    /// </summary>
    [Serializable]
    public struct RowCropTramlines
    {
        [Tooltip("Metres across the rows from one tramline to the next; 0 for none.")]
        public float spacing;

        [Tooltip("Metres between the centres of a tramline's two wheel lines.")]
        public float gauge;

        [Tooltip("Half a wheel line's width (metres); no plant stands nearer its centre.")]
        public float halfWidth;

        [Tooltip("Metres across the rows from the lattice origin to a tramline's centre.")]
        public float offset;

        public bool Any => spacing > 0f;

        /// <summary>Metres from <paramref name="across"/> to the nearest wheel line's centre.</summary>
        public float Distance(float across)
        {
            var fromLine = Mathf.Repeat(across - offset, spacing);
            var fromCentre = Mathf.Min(fromLine, spacing - fromLine);
            return Mathf.Abs(fromCentre - gauge * 0.5f);
        }

        /// <summary>Whether <paramref name="across"/> lies on a wheel line.</summary>
        public bool Covers(float across) => Any && Distance(across) < halfWidth;
    }
}
