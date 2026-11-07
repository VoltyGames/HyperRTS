namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>Fog of war for a configured match: keep the map's setting or force it.</summary>
    public enum FogOverride : byte
    {
        Map = 0,

        /// <summary>Fog of war hides what no unit sees.</summary>
        Fogged = 1,

        /// <summary>No fog: the whole map shows.</summary>
        Revealed = 2,
    }
}
