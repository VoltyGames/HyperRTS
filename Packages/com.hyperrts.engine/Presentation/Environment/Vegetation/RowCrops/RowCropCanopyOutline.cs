using System.Collections.Generic;
using UnityEngine;

namespace HyperRTS.Presentation.Environment.Vegetation.RowCrops
{
    /// <summary>
    /// A crop's outline as marching squares cut it: segments with the crop on their left seen from above, chained
    /// end to start into lines, so a wall texture runs on round corners and across blocks without a seam.
    /// </summary>
    public sealed class RowCropCanopyOutline
    {
        private readonly List<Segment> _segments = new();
        private readonly Dictionary<int, int> _startingAt = new();

        public int Count => _segments.Count;

        public Segment this[int index] => _segments[index];

        /// <summary>Adds a segment between two crossings, each named by the lattice edge it lies on.</summary>
        public void Add(int block, int from, int to, float length)
        {
            _startingAt.Add(from, _segments.Count);
            _segments.Add(new Segment(block, from, to, length));
        }

        /// <summary>
        /// Gives every segment its distance along its line in tiles. A closed loop stretches to a whole number of
        /// tiles, so its texture meets itself.
        /// </summary>
        public void Measure(float tile)
        {
            var visited = new bool[_segments.Count];
            var ending = new HashSet<int>();
            _segments.ForEach(segment => ending.Add(segment.To));
            for (var s = 0; s < _segments.Count; s++)
            {
                if (!ending.Contains(_segments[s].From))
                {
                    Measure(s, tile, visited, false);
                }
            }

            for (var s = 0; s < _segments.Count; s++)
            {
                if (!visited[s])
                {
                    Measure(s, tile, visited, true);
                }
            }
        }

        private void Measure(int first, float tile, bool[] visited, bool loop)
        {
            var line = new List<int>();
            for (var s = first; s >= 0 && !visited[s]; s = Next(s))
            {
                visited[s] = true;
                line.Add(s);
            }

            var length = 0f;
            line.ForEach(s => length += _segments[s].Length);
            var tiles = loop ? Mathf.Max(1f, Mathf.Round(length / tile)) : length / tile;
            var scale = length > 0f ? tiles / length : 0f;
            var along = 0f;
            foreach (var s in line)
            {
                var segment = _segments[s];
                segment.Start = along * scale;
                along += segment.Length;
                segment.End = along * scale;
                _segments[s] = segment;
            }
        }

        private int Next(int segment) => _startingAt.TryGetValue(_segments[segment].To, out var next) ? next : -1;

        /// <summary>One cut across a lattice square, from one crossing to the next, and its place along its line in tiles.</summary>
        public struct Segment
        {
            public readonly int Block;
            public readonly int From;
            public readonly int To;
            public readonly float Length;
            public float Start;
            public float End;

            public Segment(int block, int from, int to, float length)
            {
                Block = block;
                From = from;
                To = to;
                Length = length;
                Start = 0f;
                End = 0f;
            }
        }
    }
}
