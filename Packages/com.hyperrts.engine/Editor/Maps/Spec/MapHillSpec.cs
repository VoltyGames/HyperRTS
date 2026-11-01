using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A smooth rise; <see cref="flatTop"/> (0 to 1) of the radius stays level, for a plateau or mound top.</summary>
    [Serializable]
    [MapFeature("hill", 50, 0.75f, 0.6f, 0.35f, Radius = nameof(MapHillSpec.radius))]
    public sealed class MapHillSpec : MapFeatureSpec, IMapPoint
    {
        public MapPoint pos;
        public float radius = 20f;
        public float height = 5f;
        public float flatTop;

        public MapPoint Position
        {
            get => pos;
            set => pos = value;
        }
    }
}
