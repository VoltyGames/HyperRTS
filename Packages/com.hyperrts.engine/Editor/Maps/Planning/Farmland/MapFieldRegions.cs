using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// The land regions one farmland lays down, made once per field: its crop, and for the pieces of it too small or
    /// narrow to farm, a leftover surface (fallow, pasture). Both are picked from the field's key, so every copy of a
    /// field matches.
    /// </summary>
    public sealed class MapFieldRegions
    {
        private const int LeftoverChannel = 0x5f3759df;

        private readonly MapPlanContext _context;
        private readonly MapFarmlandKit _kit;
        private readonly int _index;
        private readonly float _angle;
        private readonly Dictionary<int, int> _fields = new();
        private readonly Dictionary<int, int> _leftovers = new();

        public MapFieldRegions(MapPlanContext context, MapFarmlandKit kit, int index, float angle)
        {
            _context = context;
            _kit = kit;
            _index = index;
            _angle = angle;
            Margin = context.Region(MapLandKind.Margin, kit.marginSurface, index);
        }

        /// <summary>The weedy band round every field.</summary>
        public int Margin { get; }

        public int Field(int field)
        {
            if (!_fields.TryGetValue(field, out var region))
            {
                var seed = Seed(field, 0);
                var crop = new System.Random(seed).Pick(_kit.crops, c => c.weight);
                _fields[field] = region = _context.Region(MapLandKind.Field, crop.surface, seed, _angle);
            }

            return region;
        }

        /// <summary>The field's leftover ground; the margin when the kit lists no leftovers.</summary>
        public int Leftover(int field)
        {
            if (_kit.leftovers.Count == 0)
            {
                return Margin;
            }

            if (!_leftovers.TryGetValue(field, out var region))
            {
                var seed = Seed(field, LeftoverChannel);
                var surface = new System.Random(seed).Pick(_kit.leftovers, c => c.weight).surface;
                _leftovers[field] = region = _context.Region(MapLandKind.Margin, surface, seed);
            }

            return region;
        }

        private int Seed(int field, int channel) => unchecked(field * 73856093 ^ _index * 19349663 ^ _context.Seed ^ channel);
    }
}
