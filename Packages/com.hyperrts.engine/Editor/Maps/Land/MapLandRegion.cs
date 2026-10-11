namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>
    /// One piece of land the planners laid out (a field, a plot's garden, a wood, a road's verge): its use, the
    /// climate surface it paints and, for crops, which way the rows run.
    /// </summary>
    public sealed class MapLandRegion
    {
        public MapLandRegion(MapLandKind kind, string surface, int seed = 0, float rowAngle = 0f)
        {
            Kind = kind;
            Surface = surface;
            Seed = seed;
            RowAngle = rowAngle;
        }

        public MapLandKind Kind { get; }

        public string Surface { get; }

        /// <summary>Stable per region in every symmetry copy, so tints and choices made from it match.</summary>
        public int Seed { get; }

        /// <summary>Direction of the crop or garden rows, degrees clockwise from north.</summary>
        public float RowAngle { get; }
    }
}
