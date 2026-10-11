using System;
using System.Collections.Generic;
using System.Linq;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>Why candidate plots were turned down, counted per reason, for the settlement's warning.</summary>
    internal sealed class MapPlotRejections
    {
        private readonly Dictionary<string, int> _counts = new();

        public int Total { get; private set; }

        public void Add(string reason)
        {
            _counts.TryGetValue(reason, out var count);
            _counts[reason] = count + 1;
            Total++;
        }

        /// <summary>"5 touching water, 3 in a base", most common first.</summary>
        public override string ToString() =>
            string.Join(", ", _counts.OrderByDescending(p => p.Value).ThenBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Value} {p.Key}"));
    }
}
