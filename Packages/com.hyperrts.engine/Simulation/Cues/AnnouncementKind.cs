using System;
using UnityEngine;

namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// Which announcer alert a <see cref="CueSlot.Announce"/> cue is, carried in <see cref="CueEvent.Variant"/>. The
    /// engine's kinds sit below <see cref="GameFirst"/>; a game numbers its own from there up.
    /// </summary>
    [Serializable]
    public struct AnnouncementKind : IEquatable<AnnouncementKind>
    {
        public const byte GameFirst = 128;

        [Tooltip("0 unit lost, 1 base under attack, 2 low power, 3 construction complete, 4 unit ready, "
            + "5 upgrade complete; a game's own kinds from 128 up.")]
        public byte Value;

        public AnnouncementKind(byte value) => Value = value;

        public static AnnouncementKind UnitLost => new(0);
        public static AnnouncementKind BaseUnderAttack => new(1);
        public static AnnouncementKind LowPower => new(2);
        public static AnnouncementKind ConstructionComplete => new(3);
        public static AnnouncementKind UnitReady => new(4);
        public static AnnouncementKind UpgradeComplete => new(5);

        /// <summary>A game's own kind, <paramref name="index"/> places past <see cref="GameFirst"/>.</summary>
        public static AnnouncementKind Game(byte index) => new((byte)(GameFirst + index));

        public readonly bool Equals(AnnouncementKind other) => Value == other.Value;

        public override readonly bool Equals(object obj) => obj is AnnouncementKind other && Equals(other);

        public override readonly int GetHashCode() => Value;

        public override readonly string ToString() => Value.ToString();

        public static bool operator ==(AnnouncementKind a, AnnouncementKind b) => a.Value == b.Value;

        public static bool operator !=(AnnouncementKind a, AnnouncementKind b) => a.Value != b.Value;
    }
}
