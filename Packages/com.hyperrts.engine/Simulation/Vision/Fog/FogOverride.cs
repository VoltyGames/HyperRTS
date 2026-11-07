namespace HyperRTS.Simulation.Vision.Fog
{
    /// <summary>Fog of war for a configured match: keep the map's setting or force it.</summary>
    public enum FogOverride : byte
    {
        Map = 0,
        On = 1,
        Off = 2,
    }
}
