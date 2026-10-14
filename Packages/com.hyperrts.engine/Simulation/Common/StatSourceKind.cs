namespace HyperRTS.Simulation.Common
{
    /// <summary>What added a <see cref="StatModifier"/>; ids only need to be unique within one kind.</summary>
    public enum StatSourceKind : byte
    {
        None = 0,
        Upgrade = 1,
        Veterancy = 2,
        Field = 3,

        /// <summary>An AI difficulty's handicap or bonus, on the AI player.</summary>
        Difficulty = 4,
    }
}
