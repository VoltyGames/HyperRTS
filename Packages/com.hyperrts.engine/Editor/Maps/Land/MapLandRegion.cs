using System;

namespace HyperRTS.Editor.Maps.Land
{
    /// <summary>
    /// One piece of land the planners laid out (a field, a plot's garden, a wood, a road's verge): its use, the
    /// climate surface it paints and, for crops and gardens, which way the rows run.
    /// </summary>
    public sealed class MapLandRegion
    {
        private readonly float? _rowAngle;

        public MapLandRegion(MapLandKind kind, string surface, int seed = 0, float? rowAngle = null)
        {
            Kind = kind;
            Surface = surface;
            Seed = seed;
            _rowAngle = rowAngle;
        }

        public MapLandKind Kind { get; }

        public string Surface { get; }

        /// <summary>Stable per region in every symmetry copy, so tints and choices made from it match.</summary>
        public int Seed { get; }

        public bool HasRows => _rowAngle.HasValue;

        /// <summary>
        /// Direction of the crop or garden rows in the authored sector, degrees clockwise from north. Regions are
        /// shared by every symmetry copy, so a copy moves it through its own transform.
        /// </summary>
        public float RowAngle => _rowAngle
            ?? throw new InvalidOperationException($"HyperRTS: the {Kind} region painting '{Surface}' has no rows.");
    }
}
