using HyperRTS.Presentation.Common;
using HyperRTS.Presentation.Environment.Vegetation.RowCrops;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment
{
    /// <summary>
    /// RowCrops.compute's per-plant placement in C# for tests to check the kernel's rules against: where a plant
    /// stands, whether it falls on a tramline, which model it draws and how it leans.
    /// </summary>
    public static class RowCropPlacement
    {
        // Match RowCrops.compute's CHANNEL_ and _SALT defines.
        private const uint VariantChannel = 9;
        private const uint LeanChannel = 10;
        private const uint LeanTurnChannel = 11;
        private const uint WanderSalt = 0x5BD1E995u;
        private const uint GrowthSalt = 0x27D4EB2Fu;

        /// <summary>A lattice spot before jitter: what decides the tile and the field a plant belongs to.</summary>
        public static Vector2 Spot(RowCropField field, int row, int plant, float rowSpacing, float plantSpacing) =>
            field.origin + field.across * (row * rowSpacing) + field.along * (plant * plantSpacing);

        public static bool Owns(Rect tile, Vector2 spot)
        {
            var insideX = spot.x >= tile.xMin && spot.x < tile.xMax;
            var insideZ = spot.y >= tile.yMin && spot.y < tile.yMax;
            return insideX && insideZ;
        }

        /// <summary>Where a plant stands: its spot jittered along and across the row, and moved by its row's wander.</summary>
        public static Vector2 Position(RowCropField field, int row, int plant, RowCrop crop)
        {
            var hash = ScatterHash.Hash(field.seed, row, plant);
            var jitter = Hashes.Fraction(ScatterHash.Channel(hash, RowCropSpots.AlongChannel)) * 2f - 1f;
            var along = jitter * crop.jitter * crop.plantSpacing;
            return Spot(field, row, plant, crop.rowSpacing, crop.plantSpacing) + field.across * Stray(field, row, plant, crop)
                + field.along * along;
        }

        /// <summary>Whether a plant falls on a tramline.</summary>
        public static bool OnTramline(RowCropField field, int row, int plant, RowCrop crop) =>
            field.tramlines.Covers(row * crop.rowSpacing + Stray(field, row, plant, crop));

        /// <summary>Metres a plant's row strays across its line: slow noise, so neighbouring rows wander together.</summary>
        public static float Wander(RowCropField field, int row, int plant, RowCrop crop)
        {
            var noise = ScatterHash.Noise(field.seed, WanderSalt, Metres(row, plant, crop) / crop.wanderLength);
            return (noise * 2f - 1f) * crop.wander;
        }

        /// <summary>The scale multiplier of the stretch of field a plant grows in.</summary>
        public static float GrowthVariation(RowCropField field, int row, int plant, RowCrop crop)
        {
            var noise = ScatterHash.Noise(field.seed, GrowthSalt, Metres(row, plant, crop) / crop.growthVariationSize);
            return Mathf.Lerp(crop.growthVariation.x, crop.growthVariation.y, noise);
        }

        /// <summary>Which of <paramref name="count"/> models a plant draws.</summary>
        public static int Variant(int seed, int row, int plant, int count)
        {
            var pick = (int)(Hashes.Fraction(ScatterHash.Channel(ScatterHash.Hash(seed, row, plant), VariantChannel)) * count);
            return Mathf.Min(pick, count - 1);
        }

        /// <summary>A plant's lean: XZ direction times the sine of its tilt; <paramref name="edge"/> 0 inside to 1 at the edge.</summary>
        public static Vector2 Lean(RowCropField field, int row, int plant, RowCrop crop, float edge)
        {
            var hash = ScatterHash.Hash(field.seed, row, plant);
            var tilt = Mathf.Lerp(crop.lean, crop.edgeLean, edge) * Mathf.Deg2Rad *
                       Hashes.Fraction(ScatterHash.Channel(hash, LeanChannel));
            var heading = Hashes.Fraction(ScatterHash.Channel(hash, LeanTurnChannel)) * 2f * Mathf.PI;
            return (field.across * Mathf.Cos(heading) + field.along * Mathf.Sin(heading)) * Mathf.Sin(tilt);
        }

        // Metres a plant stands off its row: jitter plus wander.
        private static float Stray(RowCropField field, int row, int plant, RowCrop crop)
        {
            var hash = ScatterHash.Hash(field.seed, row, plant);
            var jitter = Hashes.Fraction(ScatterHash.Channel(hash, RowCropSpots.AcrossChannel)) * 2f - 1f;
            return jitter * crop.jitter * crop.rowSpacing + Wander(field, row, plant, crop);
        }

        // Lattice coordinates in metres, so the noise keeps its size whatever the spacing.
        private static Vector2 Metres(int row, int plant, RowCrop crop) => new(row * crop.rowSpacing, plant * crop.plantSpacing);
    }
}
