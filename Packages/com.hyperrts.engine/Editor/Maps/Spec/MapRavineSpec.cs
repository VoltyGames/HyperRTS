using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A gully (balka) cut along a polyline: walls too steep to drive split lanes, a grassy rim slope eases into it,
    /// side gullies branch off and part of the floor is wooded.
    /// </summary>
    [Serializable]
    public sealed class MapRavineSpec
    {
        public List<MapPoint> points = new();
        public float width = 24f;
        public float depth = 6f;

        /// <summary>Width of the gentle grassy slope outside the steep cut (metres).</summary>
        public float rim = 10f;

        /// <summary>Side gullies branching off the main one.</summary>
        public int branches;

        /// <summary>Share of the floor and lower walls that carries trees (0 to 1).</summary>
        public float wooded = 0.5f;

        public bool shared;
    }
}
