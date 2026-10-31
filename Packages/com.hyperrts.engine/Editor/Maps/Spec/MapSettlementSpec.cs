using System;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>
    /// A village or hamlet: up to <see cref="count"/> plots from a palette settlement kit, laid out along the roads
    /// within <see cref="radius"/> of <see cref="center"/>, each with its house, yard, outbuildings, garden and fences.
    /// </summary>
    [Serializable]
    public sealed class MapSettlementSpec
    {
        public MapPoint center;
        public float radius = 60f;
        [MapKey(typeof(MapSettlementKit))]
        public string kind;
        public int count = 8;
    }
}
