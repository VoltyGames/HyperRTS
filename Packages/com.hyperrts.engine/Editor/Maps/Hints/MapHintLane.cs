using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Metrics;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Hints
{
    /// <summary>One attack path between two bases, from <see cref="From"/> to <see cref="To"/>, with its waypoints.</summary>
    public sealed class MapHintLane
    {
        public MapHintLane(MapHintBase from, MapHintBase to, MapPath path, List<Vector2> waypoints)
        {
            From = from;
            To = to;
            Path = path;
            Waypoints = waypoints;
        }

        public MapHintBase From { get; }

        public MapHintBase To { get; }

        public MapPath Path { get; }

        /// <summary>From <see cref="From"/> to <see cref="To"/>, neither base included.</summary>
        public List<Vector2> Waypoints { get; }

        /// <summary>The path's cells leaving <paramref name="origin"/>'s base; null if the lane doesn't touch that base.</summary>
        public List<int2> CellsLeaving(MapHintBase origin)
        {
            if (origin == From)
            {
                return Path.Cells;
            }

            return origin == To ? Enumerable.Reverse(Path.Cells).ToList() : null;
        }
    }
}
