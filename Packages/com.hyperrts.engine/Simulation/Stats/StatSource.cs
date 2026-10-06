using System;

namespace HyperRTS.Simulation.Stats
{
    /// <summary>Who added a <see cref="StatModifier"/>; removing one source never strips another kind's bonuses.</summary>
    public struct StatSource : IEquatable<StatSource>
    {
        public StatSourceKind Kind;

        /// <summary>The upgrade type id, field id or game-defined id.</summary>
        public int Id;

        public StatSource(StatSourceKind kind, int id)
        {
            Kind = kind;
            Id = id;
        }

        /// <summary>Promotions swap every veterancy bonus at once, so they share one source.</summary>
        public static StatSource Veterancy => new(StatSourceKind.Veterancy, 0);

        public static StatSource Upgrade(int typeId) => new(StatSourceKind.Upgrade, typeId);

        public static StatSource Field(int fieldId) => new(StatSourceKind.Field, fieldId);

        public readonly bool Equals(StatSource other) => Kind == other.Kind && Id == other.Id;

        public override readonly bool Equals(object obj) => obj is StatSource other && Equals(other);

        public override readonly int GetHashCode() => HashCode.Combine(Kind, Id);
    }
}
