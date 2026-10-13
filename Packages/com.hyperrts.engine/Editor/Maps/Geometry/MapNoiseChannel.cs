namespace HyperRTS.Editor.Maps.Geometry
{
    /// <summary>
    /// The noise fields a map samples, one channel each so no two features share a pattern by accident. The values
    /// seed the noise: changing one changes every map.
    /// </summary>
    public enum MapNoiseChannel
    {
        Relief = 0,

        /// <summary>Ravine walls and river banks, so the carved ground and the land claims follow one edge.</summary>
        Wobble = 1,
        SplatPatches = 2,
        LandEdges = 3,
        PlantClumps = 4,
        MacroTint = 6,

        /// <summary>The macro map's 10–40 m light and warmth, finer than <see cref="MacroTint"/>.</summary>
        MacroDetail = 7,
        RiverWidth = 20,
        Banks = 21,
        BaseEdges = 22,
        RavineWoods = 23,
        WoodClearings = 25,
        WoodEdges = 26,
        FieldMargins = 30,
        Verges = 31,
    }
}
