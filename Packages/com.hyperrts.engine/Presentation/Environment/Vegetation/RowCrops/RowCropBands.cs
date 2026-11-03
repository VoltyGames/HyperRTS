using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>The plant distance bands a crop tile can hold, by the kernel's rule.</summary>
    public static class RowCropBands
    {
        public const int Lods = 2;
        public const int Full = 0;
        public const int Simple = 1;

        /// <summary>Camera distances (from, to) a band draws over. Empty when it starts past the draw distance.</summary>
        public static (float From, float To) Band(int lod, RowCrop crop, float drawDistance) => lod switch
        {
            Full => (0f, Mathf.Min(ScatterBands.LastShort(crop.lod0Distance), crop.lod1Distance, drawDistance)),
            _ => (ScatterBands.FirstPast(crop.lod0Distance), Mathf.Min(crop.lod1Distance, drawDistance)),
        };

        /// <summary>Whether a band casts shadows: it ends within the shadow distance.</summary>
        public static bool CastsShadows(int lod, RowCrop crop, float drawDistance, float shadowDistance) =>
            shadowDistance > 0f && Band(lod, crop, drawDistance).To <= shadowDistance;

        /// <summary>Whether plants between <paramref name="near"/> and <paramref name="far"/> can fall in a band.</summary>
        public static bool InBand(int lod, RowCrop crop, float drawDistance, float near, float far)
        {
            var (from, to) = Band(lod, crop, drawDistance);
            if (to <= from)
            {
                return false;
            }

            return near < to && far >= from;
        }
    }
}
