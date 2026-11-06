namespace HyperRTS.Simulation.Squads
{
    /// <summary>A building squads use, such as a <see cref="ReinforcePoint"/> or <see cref="RetreatPoint"/>.</summary>
    public interface ISquadPoint
    {
        /// <summary>How far from the point a squad still counts as at it.</summary>
        float Reach { get; }
    }
}
