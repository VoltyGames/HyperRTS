using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A smooth rise; <see cref="flatTop"/> (0 to 1) of the radius stays level, for a plateau or mound top.</summary>
    [Serializable]
    public sealed class MapHillSpec
    {
        public MapPoint pos;
        public float radius = 20f;
        public float height = 5f;
        public float flatTop;
        public bool shared;
    }
}
