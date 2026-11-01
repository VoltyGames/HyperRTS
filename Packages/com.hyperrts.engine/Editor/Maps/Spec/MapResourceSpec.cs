using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A resource node holding <see cref="amount"/> supply (required, above zero).</summary>
    [Serializable]
    [MapFeature("resource", 130, 1f, 0.85f, 0.2f)]
    public sealed class MapResourceSpec : MapFeatureSpec, IMapPoint
    {
        public MapPoint pos;
        [MapKey(typeof(MapProp))]
        public string kind;
        public int amount;

        public MapPoint Position
        {
            get => pos;
            set => pos = value;
        }
    }
}
