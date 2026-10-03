namespace HyperRTS.Simulation.Match
{
    public enum PlayerControl : byte
    {
        LocalHuman = 0,
        AI = 1,

        /// <summary>Driven by another client, or idle in single player.</summary>
        Remote = 2,
    }
}
