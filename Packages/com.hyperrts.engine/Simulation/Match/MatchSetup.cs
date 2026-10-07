using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Lobby or skirmish choices applied over the baked match before its first gameplay frame. Slot i configures
    /// faction i + 1; baked slots past the end of <see cref="Slots"/> are closed.
    /// </summary>
    public struct MatchSetup : IComponentData
    {
        /// <summary>Room for 9 slots, above the 8-player maximum.</summary>
        public FixedList512Bytes<SlotSetup> Slots;

        /// <summary>Multiplies every player's baked starting resources.</summary>
        public float StartingResourceScale;

        public FogOverride Fog;

        /// <summary>Bit per closed faction, filled in when the setup is applied.</summary>
        public uint ClosedFactions;

        public bool Applied;

        public static MatchSetup Create() => new() { StartingResourceScale = 1f };

        public readonly bool IsOpen(byte faction) =>
            faction >= 1 && faction <= Slots.Length && Slots[faction - 1].Open;
    }
}
