using System;
using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// Cultivated land split into fields on a grid running at <see cref="angle"/>, with crops, margins, belts and
    /// field tracks from a climate farmland kit. No points: it fills all land nothing else uses.
    /// </summary>
    [Serializable]
    public sealed class MapFarmlandSpec
    {
        public List<MapPoint> points = new();
        public string kind;

        /// <summary>Direction the crop rows and the long sides of fields run, in degrees clockwise from north.</summary>
        public float angle;

        public bool shared;
    }
}
