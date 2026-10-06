namespace HyperRTS.Simulation.Abilities
{
    /// <summary>What an ability is aimed at.</summary>
    public enum AbilityTarget : byte
    {
        /// <summary>Fires on the caster's own position (smoke, self-heal).</summary>
        None = 0,
        Point = 1,
        Entity = 2,
    }

    /// <summary>Which entities an <see cref="AbilityTarget.Entity"/> ability may be aimed at.</summary>
    public enum AbilityTargetFilter : byte
    {
        Any = 0,
        Hostile = 1,
        Allied = 2,
    }
}
