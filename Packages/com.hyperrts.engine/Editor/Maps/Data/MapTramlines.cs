using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>
    /// A sown surface's tramlines: pairs of wheel lines of compacted soil along the rows, every spacing metres across
    /// them. The terrain paints them, row crops leave them bare and scattered plants thin out on them.
    /// </summary>
    [Serializable]
    public sealed class MapTramlines
    {
        [Tooltip("Metres across the rows from one tramline to the next; 0 for none.")]
        public float spacing;

        [Tooltip("Metres between the centres of a tramline's two wheel lines.")]
        public float gauge = 1.8f;

        [Tooltip("Width of one wheel line (metres).")]
        public float width = 0.5f;

        [Tooltip("Key of the terrain layer the wheel lines paint.")]
        [MapKey(typeof(MapClimateLayer))]
        public string layer;

        public bool Any => spacing > 0f;

        public bool Valid => !Any || (width > 0f && gauge + width < spacing && !string.IsNullOrEmpty(layer));

        /// <summary>
        /// The tramlines of a region with this seed, measured from the line through <paramref name="originAcross"/>
        /// metres across the rows in the authored sector, so every symmetry copy keeps them where they were.
        /// </summary>
        public RowCropTramlines From(int seed, float originAcross)
        {
            if (!Any)
            {
                return default;
            }

            var hash = RowCropSpots.Hash(seed, int.MinValue, int.MaxValue);
            var phase = RowCropSpots.Unit(RowCropSpots.Channel(hash, RowCropSpots.TramlineChannel));
            return new RowCropTramlines
            {
                spacing = spacing,
                gauge = gauge,
                halfWidth = width * 0.5f,
                offset = phase * spacing - originAcross,
            };
        }
    }
}
