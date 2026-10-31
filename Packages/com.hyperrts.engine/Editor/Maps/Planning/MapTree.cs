using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>One planted tree: climate tree index, position, scale, yaw in degrees and prefab variant.</summary>
    public readonly struct MapTree
    {
        /// <summary>Seeds are hashed from centimetre steps, so float noise below that can't change a variant.</summary>
        private const float SeedStep = 100f;

        public MapTree(int species, Vector2 position, float scale, float yaw, int variant)
        {
            Species = species;
            Position = position;
            Scale = scale;
            Yaw = yaw;
            Variant = variant;
        }

        /// <summary>Index into the climate's trees.</summary>
        public int Species { get; }

        public Vector2 Position { get; }

        public float Scale { get; }

        public float Yaw { get; }

        /// <summary>Index into the climate tree's prefabs, hashed from the authored-sector point.</summary>
        public int Variant { get; }

        /// <summary>A stable hash of a sector point, so every symmetry copy gets the same variant.</summary>
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
