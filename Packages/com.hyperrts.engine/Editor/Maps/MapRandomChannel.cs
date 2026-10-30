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
        Decals = 34,
        RiverMeanders = 35,
        RavineBranches = 36,

        /// <summary>Offsets each <see cref="Geometry.MapNoiseChannel"/> field, indexed by that channel.</summary>
        Noise = 37,
        PlotRegions = 38,

        /// <summary>One seed per farmland that keys its field streams.</summary>
        Farmlands = 39,
        FieldCrops = 40,
        FieldLeftovers = 41,

        /// <summary>A land region's own ground tint, indexed by the region's seed.</summary>
        RegionTints = 42,
        WarScars = 43,
    }
}
