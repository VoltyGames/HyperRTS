using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>
    /// Why candidate plots were turned down, counted per reason with the first place it happened, for the settlement's
    /// warning.
    /// </summary>
    internal sealed class MapPlotRejections
    {
        private readonly Dictionary<string, int> _counts = new();
        private readonly Dictionary<string, Vector2> _first = new();

        public int Total { get; private set; }

        public void Add(string reason, Vector2 where)
        {
            _first.TryAdd(reason, where);
            _counts.TryGetValue(reason, out var count);
            _counts[reason] = count + 1;
            Total++;
        }

        /// <summary>"5 touching water (first at (12, -40)), 3 in a base (...)", most common first.</summary>
        public override string ToString() =>
            string.Join(", ", _counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Value} {p.Key} (first at {At(_first[p.Key])})"));

        private static string At(Vector2 point) =>
            string.Format(CultureInfo.InvariantCulture, "({0:0}, {1:0})", point.x, point.y);
    }
}
