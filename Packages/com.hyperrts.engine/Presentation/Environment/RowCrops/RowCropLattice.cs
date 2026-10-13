using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// RowCrops.compute's row lattice maths in C# for the CPU and tests. A plant belongs to the one tile its spot falls
    /// in (half-open), and hashing seed and indices makes symmetric copies match.
    /// </summary>
    public static class RowCropLattice
    {
        public const uint DensityChannel = 0;
        public const uint AcrossChannel = 1;
        public const uint AlongChannel = 2;
        public const uint ScaleChannel = 3;
        public const uint YawChannel = 4;
        public const uint DropChannel = 5;
        public const uint ShadeChannel = 6;
        public const uint VariantChannel = 9;
        public const uint LeanChannel = 10;
        public const uint LeanTurnChannel = 11;

        // Seed salts so wander and patches don't share noise with each other.
        public const uint WanderSalt = 0x5BD1E995u;
        public const uint PatchSalt = 0x27D4EB2Fu;

        // Slack in lattice units so rounding never loses a spot.
        private const float Slack = 1e-4f;

        // Just above sqrt(2): the widest a turned square projects per unit of side.
        private const float Diagonal = 1.4143f;

        /// <summary>The first lattice indices (row, plant) whose spot can fall in <paramref name="rect"/>, and how many of each.</summary>
        public static (Vector2Int Min, Vector2Int Count) Range(Rect rect, RowCropField field, float rowSpacing,
            float plantSpacing)
        {
            var low = new Vector2(float.MaxValue, float.MaxValue);
            var high = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in new[] { rect.min, new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMin, rect.yMax), rect.max })
            {
                var offset = corner - field.origin;
                var lattice = new Vector2(Vector2.Dot(offset, field.across) / rowSpacing, Vector2.Dot(offset, field.along) / plantSpacing);
                low = Vector2.Min(low, lattice);
                high = Vector2.Max(high, lattice);
            }

            var min = new Vector2Int(Mathf.CeilToInt(low.x - Slack), Mathf.CeilToInt(low.y - Slack));
            var max = new Vector2Int(Mathf.FloorToInt(high.x + Slack), Mathf.FloorToInt(high.y + Slack));
            return (min, Vector2Int.Max(Vector2Int.zero, max - min + Vector2Int.one));
        }

        /// <summary>The most (rows, plants) <see cref="Range"/> gives for a square tile at any angle: the kernel's thread grid.</summary>
        public static Vector2Int MaxCount(float tileSize, float rowSpacing, float plantSpacing) =>
            new(Mathf.CeilToInt(tileSize * Diagonal / rowSpacing) + 1, Mathf.CeilToInt(tileSize * Diagonal / plantSpacing) + 1);

        /// <summary>The most plants one field can grow in a square tile (area plus a perimeter row), for buffer sizes.</summary>
        public static int PlantBound(float tileSize, float rowSpacing, float plantSpacing)
        {
            var area = tileSize * tileSize / (rowSpacing * plantSpacing);
            var edge = 4f * tileSize / Mathf.Min(rowSpacing, plantSpacing);
            return Mathf.CeilToInt(area + edge) + 1;
        }

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
            var hash = Hash(field.seed, row, plant);
            var across = (Unit(Channel(hash, AcrossChannel)) * 2f - 1f) * crop.jitter * crop.rowSpacing
                + Wander(field, row, plant, crop);
            var along = (Unit(Channel(hash, AlongChannel)) * 2f - 1f) * crop.jitter * crop.plantSpacing;
            return Spot(field, row, plant, crop.rowSpacing, crop.plantSpacing) + field.across * across + field.along * along;
        }

        /// <summary>The XZ direction a plant faces, turned up to the crop's yaw jitter.</summary>
        public static Vector2 Facing(RowCropField field, int row, int plant, RowCrop crop)
        {
            var turn = (Unit(Channel(Hash(field.seed, row, plant), YawChannel)) * 2f - 1f) * crop.yawJitter * Mathf.Deg2Rad;
            var along = crop.facesRow ? field.along : Vector2.up;
            var across = crop.facesRow ? field.across : Vector2.right;
            return along * Mathf.Cos(turn) + across * Mathf.Sin(turn);
        }

        /// <summary>Metres a plant's row strays across its line: slow noise, so neighbouring rows wander together.</summary>
        public static float Wander(RowCropField field, int row, int plant, RowCrop crop)
        {
            var noise = Noise(field.seed, WanderSalt, Metres(row, plant, crop) / crop.wanderLength);
            return (noise * 2f - 1f) * crop.wander;
        }

        /// <summary>The scale multiplier of the patch a plant grows in.</summary>
        public static float PatchScale(RowCropField field, int row, int plant, RowCrop crop)
        {
            var noise = Noise(field.seed, PatchSalt, Metres(row, plant, crop) / crop.patchSize);
            return Mathf.Lerp(crop.patchScale.x, crop.patchScale.y, noise);
        }

        /// <summary>Which of <paramref name="count"/> models a plant draws.</summary>
        public static int Variant(int seed, int row, int plant, int count)
        {
            var pick = (int)(Unit(Channel(Hash(seed, row, plant), VariantChannel)) * count);
            return Mathf.Min(pick, count - 1);
        }

        /// <summary>A plant's lean: XZ direction times the sine of its tilt; <paramref name="edge"/> 0 inside to 1 at the edge.</summary>
        public static Vector2 Lean(RowCropField field, int row, int plant, RowCrop crop, float edge)
        {
            var hash = Hash(field.seed, row, plant);
            var tilt = Mathf.Lerp(crop.lean, crop.edgeLean, edge) * Mathf.Deg2Rad * Unit(Channel(hash, LeanChannel));
            var heading = Unit(Channel(hash, LeanTurnChannel)) * 2f * Mathf.PI;
            return (field.across * Mathf.Cos(heading) + field.along * Mathf.Sin(heading)) * Mathf.Sin(tilt);
        }

        /// <summary>Smooth value noise from 0 to 1 on an integer grid, as RowCrops.compute's Noise.</summary>
        public static float Noise(int seed, uint salt, Vector2 point)
        {
            var salted = unchecked((int)((uint)seed ^ salt));
            var (x, z) = (Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            var (fx, fz) = (point.x - x, point.y - z);
            var (ux, uz) = (fx * fx * (3f - 2f * fx), fz * fz * (3f - 2f * fz));
            var low = Mathf.Lerp(Unit(Hash(salted, x, z)), Unit(Hash(salted, x + 1, z)), ux);
            var high = Mathf.Lerp(Unit(Hash(salted, x, z + 1)), Unit(Hash(salted, x + 1, z + 1)), ux);
            return Mathf.Lerp(low, high, uz);
        }

        public static uint Hash(int seed, int row, int plant) =>
            Pcg(unchecked((uint)seed) ^ Pcg(unchecked((uint)row) ^ Pcg(unchecked((uint)plant))));

        /// <summary>An independent random value per channel from one plant hash.</summary>
        public static uint Channel(uint hash, uint channel) => Pcg(unchecked(hash + channel * 0x9E3779B9u));

        /// <summary>A hash as a value from 0 up to (not including) 1.</summary>
        public static float Unit(uint value) => (value >> 8) * (1f / 16777216f);

        // Lattice coordinates in metres, so the noise keeps its size whatever the spacing.
        private static Vector2 Metres(int row, int plant, RowCrop crop) => new(row * crop.rowSpacing, plant * crop.plantSpacing);

        private static uint Pcg(uint value)
        {
            unchecked
            {
                var state = value * 747796405u + 2891336453u;
                var word = ((state >> (int)((state >> 28) + 4u)) ^ state) * 277803737u;
                return (word >> 22) ^ word;
            }
        }
    }
}
