using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A road of a climate road class along a polyline the builder smooths into curves. Ends near another road join
    /// it; ends on the playable edge run on into the backdrop.
    /// </summary>
    [Serializable]
    public sealed class MapRoadSpec
    {
        public List<MapPoint> points = new();

        /// <summary>Road class key in the climate (its width, surface, mesh material, verge and junction rank).</summary>
        [MapKey(typeof(MapRoadClass))]
        public string kind;

        public bool shared;
    }
}
