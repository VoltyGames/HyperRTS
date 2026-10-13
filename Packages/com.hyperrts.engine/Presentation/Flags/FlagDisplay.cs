using HyperRTS.Simulation.Common;

namespace HyperRTS.Presentation.Flags
{
    /// <summary>Which faction's flag a building flies and how high, from its owner and any capture under way.</summary>
    public static class FlagDisplay
    {
        /// <summary>
        /// The owner's flag lowers and the capturer's rises as <paramref name="progress"/> goes 0 to 1: over the first
        /// and second half for an owned building, over the whole for a neutral one. Neutral and idle shows none.
        /// </summary>
        public static byte Resolve(byte owner, byte capturer, float progress, out float hoist)
        {
            var capturing = capturer != Faction.Neutral && capturer != owner && progress > 0f;
            if (!capturing)
            {
                hoist = owner == Faction.Neutral ? 0f : 1f;
                return owner;
            }

            var done = progress >= 1f ? 1f : progress;
            if (owner == Faction.Neutral)
            {
                hoist = done;
                return capturer;
            }

            if (done < 0.5f)
            {
                hoist = 1f - done * 2f;
                return owner;
            }

            hoist = done * 2f - 1f;
            return capturer;
        }
    }
}
