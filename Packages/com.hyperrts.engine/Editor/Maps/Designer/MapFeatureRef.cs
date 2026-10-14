using System;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>One feature of a spec: its kind's list and its index in that list.</summary>
    public readonly struct MapFeatureRef : IEquatable<MapFeatureRef>
    {
        public MapFeatureRef(MapFeatureKind kind, int index)
        {
            Kind = kind;
            Index = index;
        }

        public static MapFeatureRef None => default;

        public MapFeatureKind Kind { get; }

        public int Index { get; }

        public bool IsNone => Kind == null;

        public bool Equals(MapFeatureRef other) => Kind == other.Kind && Index == other.Index;

        public override bool Equals(object obj) => obj is MapFeatureRef other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(Kind?.Field, Index);

        public override string ToString() => IsNone ? "none" : $"{Kind.Label} {Index + 1}";
    }
}
