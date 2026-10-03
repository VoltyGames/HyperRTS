namespace HyperRTS.Simulation.Match
{
    /// <summary>Who drives a player slot.</summary>
    public enum PlayerControl : byte
    {
        LocalHuman = 0,
        AI = 1,

        /// <summary>Driven by another client, or idle in single player.</summary>
        Remote = 2,
    }
}
