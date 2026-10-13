using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// One planted tree: climate tree index, map position, scale, yaw in degrees and the seed its model variant is
    /// picked by.
    /// </summary>
    public readonly struct MapTree
    {
        /// <summary>Seeds are hashed from centimetre steps, so float noise below that can't change a variant.</summary>
        private const float SeedStep = 100f;

        public MapTree(int species, Vector2 position, float scale, float yaw, uint seed)
        {
            Species = species;
            Position = position;
            Scale = scale;
            Yaw = yaw;
            Seed = seed;
        }

        /// <summary>Index into the climate's trees.</summary>
        public int Species { get; }

        public Vector2 Position { get; }

        public float Scale { get; }

        public float Yaw { get; }

        /// <summary>Hashed from the authored-sector point, so every symmetry copy of a tree gets the same variant.</summary>
        public uint Seed { get; }

        /// <summary>A well-mixed hash of a sector point, the same on every run.</summary>
        public static uint SeedAt(Vector2 sectorPoint)
        {
            var x = (uint)Mathf.RoundToInt(sectorPoint.x * SeedStep);
            var z = (uint)Mathf.RoundToInt(sectorPoint.y * SeedStep);
            var hash = x * 0x9E3779B1u ^ (z + 0x7F4A7C15u) * 0x85EBCA77u;
            hash ^= hash >> 15;
            hash *= 0xC2B2AE3Du;
            hash ^= hash >> 13;
            hash *= 0x27D4EB2Fu;
            return hash ^ (hash >> 16);
        }
    }
}
