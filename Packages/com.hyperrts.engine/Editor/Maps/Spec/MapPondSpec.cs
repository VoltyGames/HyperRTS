using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A pond: still water inside a polygon (its shoreline) with a gentle basin, a wet margin and, on one edge, an
    /// earth dam raised above the ground. Reeds come from the climate; willows from the shore surface's trees.
    /// </summary>
    [Serializable]
    [MapFeature("pond", 65, 0.3f, 0.6f, 0.9f, Closed = true)]
    public sealed class MapPondSpec : MapFeatureSpec, IMapLine
    {
        public List<MapPoint> points = new();

        /// <summary>Depth of the deepest point below the water level.</summary>
        public float depth = 2f;

        /// <summary>Metres the basin and the ground outside ease over; the banks are gentle where this is large.</summary>
        public float bankWidth = 8f;

        /// <summary>Surface of the wet margin round the water, whose trees grow willows.</summary>
        [MapKey(typeof(MapClimateSurface))]
        public string shore;

        /// <summary>Width of the wet margin above the water (metres).</summary>
        public float shoreWidth = 6f;

        /// <summary>The dam is the edge from this point to the next one; -1 for a pond without a dam.</summary>
        public int dam = -1;

        /// <summary>Width of the dam's flat top (metres).</summary>
        public float damWidth = 5f;

        /// <summary>Height of the dam's top above the water level, or above the ground beside it if that is higher.</summary>
        public float damHeight = 1.5f;

        /// <summary>Surface of the dam's body and track on top; empty when the pond has no dam.</summary>
        [MapKey(typeof(MapClimateSurface), Optional = true)]
        public string damSurface;

        public List<MapPoint> Points => points;
    }
}
