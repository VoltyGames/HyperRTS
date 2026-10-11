using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A river cut below the water level, with sloped banks out to <see cref="valleyWidth"/>.</summary>
    [Serializable]
    public sealed class MapRiverSpec
    {
        public List<MapPoint> points = new();
        public float width = 14f;

        /// <summary>Depth of the bed below the water level.</summary>
        public float depth = 2f;

        /// <summary>Full width of the valley the river runs in; the ground eases down to the banks across it.</summary>
        public float valleyWidth = 60f;

        public List<MapCrossingSpec> crossings = new();
        public bool shared;
    }
}
