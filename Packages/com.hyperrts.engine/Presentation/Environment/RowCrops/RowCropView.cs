using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>The distance bands a crop tile can hold, by the kernel's rule.</summary>
    public static class RowCropView
    {
        public const int Lods = 3;
        public const int Full = 0;
        public const int Simple = 1;
        public const int Card = 2;

        /// <summary>Camera distances (from, to) a band draws over. Empty when it starts past the draw distance.</summary>
        public static (float From, float To) Band(int lod, RowCrop crop, float drawDistance) => lod switch
        {
            Full => (0f, Mathf.Min(crop.lod0Distance, drawDistance)),
            Simple => (crop.lod0Distance, Mathf.Min(crop.lod1Distance, drawDistance)),
            _ => (crop.lod1Distance, drawDistance),
        };

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
