using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A player start: the palette's start kit is placed here facing <see cref="yaw"/>.</summary>
    [Serializable]
    public sealed class MapSpawnSpec
    {
        public MapPoint pos;
        public float yaw;
    }
}
