namespace HyperRTS.Editor.Maps
{
    /// <summary>
    /// The random streams of authored features, one channel per kind of choice. The values seed the streams: changing
    /// one changes every map.
    /// </summary>
    public enum MapRandomChannel
    {
        ResourceYaw = 1,
        PropYaw = 2,
        LineYaw = 3,
        Scatters = 4,
        TreeRows = 11,
        RavineTrees = 24,
        WoodTrees = 27,
        BeltTrees = 28,
        BeltGaps = 29,
        SurfaceGrowth = 31,
        Settlements = 32,
        Plots = 33,
    }
}
