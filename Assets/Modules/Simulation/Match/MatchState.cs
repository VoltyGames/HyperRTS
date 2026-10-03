using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
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
    }
}
