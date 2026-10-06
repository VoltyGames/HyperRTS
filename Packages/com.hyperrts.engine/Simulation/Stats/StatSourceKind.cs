namespace HyperRTS.Simulation.Stats
{
    /// <summary>What added a <see cref="StatModifier"/>; ids only need to be unique within one kind.</summary>
    public enum StatSourceKind : byte
    {
        None = 0,
        Upgrade = 1,
        Veterancy = 2,
        Field = 3,

        /// <summary>Kinds from here up belong to games; the engine never uses them.</summary>
        Custom = 128,
    }
}
