using System;

namespace HyperRTS.Simulation.Navigation
{
    /// <summary>What a <see cref="NavGrid"/> cell is made of; an agent passes when it shares a flag with its layer.</summary>
    [Flags]
    public enum NavSurface : byte
    {
        Blocked = 0,
        Land = 1,
        Water = 2,

        /// <summary>Land laid over the cell by a deck <see cref="NavArea"/>; ground stands at its deck height.</summary>
        Deck = 4,
    }
}
