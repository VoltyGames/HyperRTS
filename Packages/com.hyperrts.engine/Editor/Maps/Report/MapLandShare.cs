using System;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>One land kind's share of the playable area.</summary>
    [Serializable]
    public sealed class MapLandShare
    {
        public string kind;
        public float share;
    }
}
