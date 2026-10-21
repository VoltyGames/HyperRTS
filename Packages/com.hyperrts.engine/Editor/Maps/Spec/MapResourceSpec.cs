using System;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Library;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A resource node holding <see cref="amount"/> supply (required, above zero).</summary>
    [Serializable]
    public sealed class MapResourceSpec
    {
        public MapPoint pos;
        [MapKey(typeof(MapProp))]
        public string kind;
        public int amount;
        public bool shared;
    }
}
