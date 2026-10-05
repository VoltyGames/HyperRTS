using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Whether the match is still being played.</summary>
    public enum MatchPhase : byte
    {
        Playing = 0,
        Ended = 1,
    }

    /// <summary>Singleton match outcome; <see cref="WinningTeam"/> is 0 for a draw.</summary>
    public struct MatchState : IComponentData
    {
        public MatchPhase Phase;
        public byte WinningTeam;

        /// <summary>Bit per faction that has owned a <see cref="VictoryCritical"/> entity; only those can lose.</summary>
        public uint Contenders;
    }
}
