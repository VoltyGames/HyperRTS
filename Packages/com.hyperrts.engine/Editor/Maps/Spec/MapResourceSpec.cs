using System;

namespace HyperRTS.Editor.Maps.Spec
{
    /// <summary>A resource node; <see cref="amount"/> overrides the prefab's when above zero.</summary>
    [Serializable]
    public sealed class MapResourceSpec
    {
        public MapPoint pos;
        public string kind;
        public int amount;
        public bool shared;
    }
}
