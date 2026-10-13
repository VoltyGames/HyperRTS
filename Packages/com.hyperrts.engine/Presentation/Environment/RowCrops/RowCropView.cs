using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// The CPU side of drawing crops: a tile's bounds, how near and far it is from the camera, and which distance
    /// bands (full mesh, simple mesh, card) it can hold, so buffers are sized only for the bands in view. The kernel
    /// picks each plant's band by the same rule: full below lod0Distance, simple below lod1Distance, card up to the
    /// draw distance.
    /// </summary>
    public static class RowCropView
    {
        public const int Lods = 3;
        public const int Full = 0;
        public const int Simple = 1;
        public const int Card = 2;

        /// <summary>Horizontal metres a plant can stand outside its tile: its jitter is under half a spacing.</summary>
        public const float Overhang = 1f;

        /// <summary>A tile's box: its square widened by the overhang, from the lowest ground to the tallest plant.</summary>
        public static Bounds TileBounds(Rect tile, float groundMin, float groundMax, float plantHeight)
        {
            var min = new Vector3(tile.xMin - Overhang, groundMin, tile.yMin - Overhang);
            var max = new Vector3(tile.xMax + Overhang, groundMax + plantHeight, tile.yMax + Overhang);
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        /// <summary>Distance from the camera to the nearest and the farthest point of a box.</summary>
        public static (float Near, float Far) Distances(Bounds bounds, Vector3 camera)
        {
            var near = Mathf.Sqrt(bounds.SqrDistance(camera));
            var reach = Vector3.Max(bounds.max - camera, camera - bounds.min);
            return (near, reach.magnitude);
        }

        /// <summary>The camera distances (from, to) a band draws over at a draw distance; empty when it starts past it.</summary>
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
