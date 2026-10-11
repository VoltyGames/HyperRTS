using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A road of a climate road class along a polyline the builder smooths into curves. Ends near another road join
    /// it; ends on the playable edge run on into the margin.
    /// </summary>
    [Serializable]
    public sealed class MapRoadSpec
    {
        public List<MapPoint> points = new();

        /// <summary>Road class key in the climate (its width, surface, mesh material, verge and junction rank).</summary>
        public string kind;

        public bool shared;
    }
}
