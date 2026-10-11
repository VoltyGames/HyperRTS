using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Symmetry
{
    /// <summary>
    /// How the authored sector fills the map: the copies (identity first), which points the sector covers, and how
    /// copies number their player slots.
    /// </summary>
    public sealed class MapSymmetry
    {
        public const string Rotate180 = "rotate-180";
        public const string MirrorXName = "mirror-x";
        public const string MirrorZName = "mirror-z";
        private const string RotatePrefix = "rotate-";
        private const float Tolerance = 0.5f;

        private readonly Func<Vector2, bool> _inSector;

        private MapSymmetry(string name, IReadOnlyList<MapTransform> copies, Func<Vector2, bool> inSector)
        {
            Name = name;
            Copies = copies;
            _inSector = inSector;
        }

        public string Name { get; }

        /// <summary>Every copy of the sector, the authored one (identity) first.</summary>
        public IReadOnlyList<MapTransform> Copies { get; }

        /// <summary>Parses a spec's <c>symmetry</c>: rotate-180, mirror-x, mirror-z or rotate-N (N = 3 to 8).</summary>
        public static MapSymmetry Parse(string name)
        {
            switch (name)
            {
                case MirrorXName:
                    return new MapSymmetry(name, new[] { MapTransform.Identity, MapTransform.MirrorX },
                        p => p.x <= Tolerance);
                case MirrorZName:
                    return new MapSymmetry(name, new[] { MapTransform.Identity, MapTransform.MirrorZ },
                        p => p.y <= Tolerance);
                case Rotate180:
                    return new MapSymmetry(name, new[] { MapTransform.Identity, MapTransform.Rotation(180f) },
                        p => p.y <= Tolerance);
            }

            if (name != null && name.StartsWith(RotatePrefix, StringComparison.Ordinal)
                && int.TryParse(name.Substring(RotatePrefix.Length), NumberStyles.None, CultureInfo.InvariantCulture,
                    out var count) && count >= 3 && count <= 8)
            {
                return Rotations(name, count);
            }

            throw new ArgumentException($"Unknown symmetry '{name}'. Use rotate-180, mirror-x, mirror-z or rotate-3 to rotate-8.");
        }

        /// <summary>Whether an authored point lies in the sector, allowing half a metre over its edges.</summary>
        public bool InSector(Vector2 point) => _inSector(point);

        /// <summary>Copies interleave slots, so slots 1 and 2 always face each other in a two-player game.</summary>
        public int Slot(int sectorSpawn, int copy) => sectorSpawn * Copies.Count + copy + 1;

        /// <summary>FFA gives every slot its own team; otherwise each copy of the sector is one team.</summary>
        public static int Team(bool freeForAll, int slot, int copy) => freeForAll ? slot : copy + 1;

        /// <summary>
        /// True when every copy maps the point set onto itself, so a shared feature stays symmetric: a single point
        /// must be the centre or on the mirror axis.
        /// </summary>
        public bool IsFixed(IReadOnlyList<Vector2> points)
        {
            foreach (var copy in Copies)
            {
                foreach (var point in points)
                {
                    if (!ContainsNear(points, copy.Apply(point)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static MapSymmetry Rotations(string name, int count)
        {
            var copies = new MapTransform[count];
            var wedge = 360f / count;
            for (var i = 0; i < count; i++)
            {
                copies[i] = i == 0 ? MapTransform.Identity : MapTransform.Rotation(i * wedge);
            }

            return new MapSymmetry(name, copies, p => InWedge(p, wedge));
        }

        private static bool InWedge(Vector2 point, float wedge)
        {
            if (point.magnitude <= Tolerance)
            {
                return true;
            }

            var angle = Mathf.Repeat(Mathf.Atan2(point.y, point.x) * Mathf.Rad2Deg, 360f);
            var slack = Tolerance / point.magnitude * Mathf.Rad2Deg;
            return angle <= wedge + slack || angle >= 360f - slack;
        }

        private static bool ContainsNear(IReadOnlyList<Vector2> points, Vector2 target)
        {
            foreach (var point in points)
            {
                if ((point - target).sqrMagnitude <= Tolerance * Tolerance)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
