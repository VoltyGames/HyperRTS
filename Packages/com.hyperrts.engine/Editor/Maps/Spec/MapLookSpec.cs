using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A place the look captures (<c>map look</c>) frame from the match camera at every zoom, to judge light, plants
    /// and water in Play mode. It changes nothing on the map and is never copied by the symmetry.
    /// </summary>
    [Serializable]
    [MapFeature("look", 150, 0.95f, 0.95f, 0.95f)]
    public sealed class MapLookSpec : IMapPoint
    {
        public string name;
        public MapPoint pos;
        public float yaw;

        /// <summary>Keeps the fog of war on, to judge how fogged ground reads; other looks see the whole map.</summary>
        public bool fog;

        public MapPoint Position
        {
            get => pos;
            set => pos = value;
        }
    }
}
