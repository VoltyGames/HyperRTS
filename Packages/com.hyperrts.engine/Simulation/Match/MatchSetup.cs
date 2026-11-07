using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>
    /// Lobby or skirmish choices applied over the baked match before its first gameplay frame. Slot i configures
    /// owner i + 1; baked slots past the end of <see cref="Slots"/> are closed.
    /// </summary>
    public struct MatchSetup : IComponentData
    {
        /// <summary>Room for 9 slots, above the 8-player maximum.</summary>
        public FixedList512Bytes<SlotSetup> Slots;

        /// <summary>Multiplies every player's baked starting resources.</summary>
        public float StartingResourceScale;

        public FogOverride Fog;

        /// <summary>Copied to <see cref="MatchRules.StrategicAssets"/>.</summary>
        public bool StrategicAssets;

        /// <summary>The map weather's key the scene switches to when it loads; empty keeps the map's own.</summary>
        public FixedString32Bytes Weather;

        /// <summary>Bit per closed owner, filled in when the setup is applied.</summary>
        public uint ClosedOwners;

        /// <summary>
        /// Match seed shared by every peer and recorded in replays; the AI module derives each bot's attack lane choice
        /// from it and the bot's owner.
        /// </summary>
        public uint Seed;

        public bool Applied;

        public static MatchSetup Create() => new() { StartingResourceScale = 1f, StrategicAssets = true };

        public readonly bool IsOpen(byte owner) =>
            owner >= 1 && owner <= Slots.Length && Slots[owner - 1].Open;
    }
}
