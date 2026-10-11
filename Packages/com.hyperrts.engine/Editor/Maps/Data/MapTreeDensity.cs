using System;

namespace HyperRTS.Editor.Maps.Data
{
    /// <summary>How many of one tree grow per hectare of a surface.</summary>
    [Serializable]
    public sealed class MapTreeDensity
    {
        public string tree;
        public float perHectare = 100f;
    }
}
