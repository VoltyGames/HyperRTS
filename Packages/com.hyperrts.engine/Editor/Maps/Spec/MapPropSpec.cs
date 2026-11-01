using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>One palette prefab: a neutral structure (derrick, outpost) or a piece of scenery.</summary>
    [Serializable]
    [MapFeature("prop", 120, 0.85f, 0.8f, 0.7f)]
    public sealed class MapPropSpec : MapFeatureSpec, IMapPoint
    {
        public MapPoint pos;
        public float yaw;
        [MapKey(typeof(MapProp))]
        public string kind;

        public MapPoint Position
        {
            get => pos;
            set => pos = value;
        }
    }
}
