using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A resource node holding <see cref="amount"/> supply (required, above zero).</summary>
    [Serializable]
    public sealed class MapResourceSpec
    {
        public MapPoint pos;
        public string kind;
        public int amount;
        public bool shared;
    }
}
