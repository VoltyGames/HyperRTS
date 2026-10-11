namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>What a piece of land is used for; every ground, tree, plant and prop pass reads it.</summary>
    public enum MapLandKind : byte
    {
        Open,
        Water,
        Bank,
        Floodplain,
        Road,
        Verge,
        Pad,
        Base,
        Ravine,
        RavineRim,
        Yard,
        Garden,
        Back,
        Area,
        Wood,
        WoodEdge,
        Belt,
        Field,
        Margin,
        Meadow,
    }
}
