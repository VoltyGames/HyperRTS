using System;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A village or hamlet: up to <see cref="count"/> plots from a palette settlement kit, laid out along the roads
    /// within <see cref="radius"/> of <see cref="center"/>, each with its house, yard, outbuildings, garden and fences.
    /// </summary>
    [Serializable]
    [MapFeature("settlement", 60, 0.95f, 0.45f, 0.3f, Radius = nameof(MapSettlementSpec.radius))]
    public sealed class MapSettlementSpec : MapFeatureSpec, IMapPoint
    {
        public MapPoint center;
        public float radius = 60f;
        [MapKey(typeof(MapSettlementKit))]
        public string kind;
        public int count = 8;

        public MapPoint Position
        {
            get => center;
            set => center = value;
        }
    }
}
