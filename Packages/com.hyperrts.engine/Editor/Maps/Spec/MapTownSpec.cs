using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A settlement: <see cref="count"/> buildings from a palette town kit lined up along the roads in range.</summary>
    [Serializable]
    public sealed class MapTownSpec
    {
        public MapPoint center;
        public float radius;
        public string kind;
        public int count;
        public bool shared;
    }
}
