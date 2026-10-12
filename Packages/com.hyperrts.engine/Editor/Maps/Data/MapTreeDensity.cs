using System;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How many of one tree grow per hectare of a surface.</summary>
    [Serializable]
    public sealed class MapTreeDensity
    {
        [MapKey(typeof(MapClimateTree))]
        public string tree;
        public float perHectare = 100f;
    }
}
