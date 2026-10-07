using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Runtime settings of one baked player slot; see <see cref="MatchSetup"/>.</summary>
    public struct SlotSetup
    {
        /// <summary>Closed slots lose their player and everything it owns.</summary>
        public bool Open;

        public PlayerControl Control;
        public byte Team;

        /// <summary>Linear RGBA team colour.</summary>
        public float4 Color;

        public FixedString32Bytes Name;
        public AIDifficulty Difficulty;

        /// <summary>Copied to <c>PlayerSide</c>: the game's army id.</summary>
        public byte Side;
    }
}
