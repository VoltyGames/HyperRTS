using System;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Climate
{
    /// <summary>
    /// Stripes a harvest leaves along a surface's rows, one per pass of the combine: straw lies on them, so the ground
    /// tint lightens and fewer plants stand.
    /// </summary>
    [Serializable]
    public sealed class MapSwaths
    {
        [Tooltip("Metres across the rows from one swath to the next, the combine's cut width; 0 for none.")]
        public float spacing;

        [Tooltip("Width of one swath (metres).")]
        public float width = 2.5f;

        [Tooltip("Lightness a swath adds to the ground tint; below 0 darkens.")]
        public float light = 0.04f;

        [Tooltip("Share of the surface's plants still standing on a swath, 0 to 1.")]
        [Range(0f, 1f)]
        public float plants = 1f;

        public bool Any => spacing > 0f;

        public bool Valid => !Any || (width > 0f && width < spacing && plants is >= 0f and <= 1f);

        /// <summary>Metres a region's swaths are slid across its rows, from its seed.</summary>
        public float Offset(int seed) => Mathf.Repeat(seed * 0.618034f, 1f) * spacing;

        /// <summary>A region's swaths for its row crop, measured from <paramref name="originAcross"/> across its rows in the sector, so the plants thin on the stripes the ground tint lightens.</summary>
        public RowCropSwaths From(int seed, float originAcross)
        {
            if (!Any)
            {
                return default;
            }

            return new RowCropSwaths
            {
                spacing = spacing,
                halfWidth = width * 0.5f,
                plants = plants,
                offset = originAcross + Offset(seed),
            };
        }

        /// <summary>0 between swaths to 1 on one, softened over <paramref name="soft"/> metres.</summary>
        public float Cover(float across, float soft)
        {
            var fromCentre = Mathf.Abs(Mathf.Repeat(across, spacing) - spacing * 0.5f);
            return 1f - MapGeometry.Smooth((fromCentre - width * 0.5f) / soft + 0.5f);
        }
    }
}
