using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A player start: the palette's start kit is placed here facing <see cref="yaw"/>.</summary>
    [Serializable]
    [MapFeature("spawn", 140, 0.2f, 0.6f, 1f)]
    public sealed class MapSpawnSpec : IMapPoint
    {
        public MapPoint pos;
        public float yaw;

        public MapPoint Position
        {
            get => pos;
            set => pos = value;
        }
    }
}
