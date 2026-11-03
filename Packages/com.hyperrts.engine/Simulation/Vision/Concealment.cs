namespace HyperRTS.Simulation.Vision
{
    /// <summary>What keeps an entity from a team that has it in sight.</summary>
    public enum Concealment : byte
    {
        None = 0,

        /// <summary>Still in woods, crops or ruins: seen only by detectors and enemy ground units close by.</summary>
        Concealed = 1,

        /// <summary>Seen only by detectors.</summary>
        Stealthed = 2,
    }
}
