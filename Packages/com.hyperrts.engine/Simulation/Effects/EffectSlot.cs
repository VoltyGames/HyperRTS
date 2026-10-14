namespace HyperRTS.Simulation.Effects
{
    /// <summary>What an entity's effect is for. Games add their own slots from <see cref="Custom"/> upward.</summary>
    public enum EffectSlot : byte
    {
        None = 0,

        /// <summary>Each shot, at the muzzle.</summary>
        Fire = 1,
        Death = 2,
        Ability = 3,
        Custom = 64,
    }
}
