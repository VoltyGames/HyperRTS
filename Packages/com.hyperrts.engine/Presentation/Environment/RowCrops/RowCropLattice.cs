using UnityEngine;

namespace HyperRTS.Presentation.Environment.RowCrops
{
    /// <summary>
    /// The row lattice maths RowCrops.compute runs, kept here in C# for the CPU and the tests. A plant's spot is
    /// <c>origin + across * row * rowSpacing + along * plant * plantSpacing</c>; it belongs to the one tile its spot
    /// falls in (half-open), so neighbouring tiles never both grow it or both skip it. Everything else about a plant
    /// (jitter, size, turn, whether it grows at all) comes from a hash of the field seed and its lattice indices, so a
    /// symmetric copy's plants match the original's.
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

        // Lattice units a spot may sit outside the projected tile and still be tried, so rounding never loses one.
        private const float Slack = 1e-4f;

        // Just above the square root of two: the widest a turned square projects onto any axis, per unit of side.
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

        /// <summary>Where a plant stands: its spot moved by up to the crop's jitter along and across the row.</summary>
        public static Vector2 Position(RowCropField field, int row, int plant, RowCrop crop)
        {
            var hash = Hash(field.seed, row, plant);
            var across = (Unit(Channel(hash, AcrossChannel)) * 2f - 1f) * crop.jitter * crop.rowSpacing;
            var along = (Unit(Channel(hash, AlongChannel)) * 2f - 1f) * crop.jitter * crop.plantSpacing;
            return Spot(field, row, plant, crop.rowSpacing, crop.plantSpacing) + field.across * across + field.along * along;
        }

        /// <summary>The XZ direction a plant faces: along its row, turned up to the crop's yaw jitter.</summary>
        public static Vector2 Facing(RowCropField field, int row, int plant, RowCrop crop)
        {
            var turn = (Unit(Channel(Hash(field.seed, row, plant), YawChannel)) * 2f - 1f) * crop.yawJitter * Mathf.Deg2Rad;
            return field.along * Mathf.Cos(turn) + field.across * Mathf.Sin(turn);
        }

        public static uint Hash(int seed, int row, int plant) =>
            Pcg(unchecked((uint)seed) ^ Pcg(unchecked((uint)row) ^ Pcg(unchecked((uint)plant))));

        /// <summary>An independent random value per channel from one plant hash.</summary>
        public static uint Channel(uint hash, uint channel) => Pcg(unchecked(hash + channel * 0x9E3779B9u));

        /// <summary>A hash as a value from 0 up to (not including) 1.</summary>
        public static float Unit(uint value) => (value >> 8) * (1f / 16777216f);

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
