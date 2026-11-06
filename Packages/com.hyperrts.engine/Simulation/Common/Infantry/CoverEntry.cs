using Unity.Mathematics;

namespace HyperRTS.Simulation.Common.Infantry
{
    /// <summary>What one <see cref="CoverKind"/> does for a member standing in it, from the front.</summary>
    public struct CoverEntry
    {
        /// <summary>Share of damage taken.</summary>
        public float DamageTaken;

        /// <summary>Share of suppression taken.</summary>
        public float SuppressionTaken;

        /// <summary>How much a squad wants a spot of this kind, scored against the metres from its place in the ring.</summary>
        public float Preference;

        /// <summary>No cover: all of a hit, and nothing to want.</summary>
        public static CoverEntry Open => new() { DamageTaken = 1f, SuppressionTaken = 1f };

        /// <summary>The shares a hit takes when it ignores <paramref name="piercing"/> (0 to 1) of the protection.</summary>
        public readonly Exposure Pierced(float piercing)
        {
            var ignored = math.saturate(piercing);
            return new Exposure
            {
                Damage = math.lerp(DamageTaken, 1f, ignored),
                Suppression = math.lerp(SuppressionTaken, 1f, ignored),
            };
        }
    }
}
