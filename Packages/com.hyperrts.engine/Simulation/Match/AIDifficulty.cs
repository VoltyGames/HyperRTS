namespace HyperRTS.Simulation.Match
{
    /// <summary>Skill preset of an AI player slot; <c>MatchAuthoring</c> maps each to an <c>AITuning</c>.</summary>
    public enum AIDifficulty : byte
    {
        Easy = 0,
        Normal = 1,
        Hard = 2,
        Expert = 3,

        /// <summary>Hardest preset; its tuning may grant an income bonus.</summary>
        Brutal = 4,
    }
}
