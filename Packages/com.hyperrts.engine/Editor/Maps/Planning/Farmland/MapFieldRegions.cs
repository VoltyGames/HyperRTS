using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Land;

namespace HyperRTS.Editor.Maps.Planning.Farmland
{
    /// <summary>
    /// The land regions one farmland lays down, made once per field: its crop, its headland and, for pieces too small
    /// or narrow to farm, a leftover surface. All are picked from the field's key, so every copy matches.
    /// </summary>
    public sealed class MapFieldRegions
    {
        private readonly MapPlanContext _context;
        private readonly MapFarmlandKit _kit;
        private readonly int _seed;
        private readonly float _angle;
        private readonly Dictionary<int, (string Surface, int Seed, int Region)> _fields = new();
        private readonly Dictionary<int, int> _headlands = new();
        private readonly Dictionary<int, int> _leftovers = new();

        public MapFieldRegions(MapPlanContext context, MapFarmlandKit kit, int index, float angle)
        {
            _context = context;
            _kit = kit;
            _seed = context.Random(MapRandomChannel.Farmlands, index).Next();
            _angle = angle;
            Margin = context.Region(MapLandKind.Margin, kit.marginSurface, index);
        }

        /// <summary>The weedy band round every field.</summary>
        public int Margin { get; }

        public int Field(int field) => Sown(field).Region;

        /// <summary>The field's ends: its crop and seed, rows turned a quarter.</summary>
        public int Headland(int field)
        {
            if (!_headlands.TryGetValue(field, out var region))
            {
                var (surface, seed, _) = Sown(field);
                _headlands[field] = region = _context.Region(MapLandKind.Field, surface, seed, _angle + 90f);
            }

            return region;
        }

        private (string Surface, int Seed, int Region) Sown(int field)
        {
            if (!_fields.TryGetValue(field, out var sown))
            {
                var random = MapRandom.Stream(_seed, MapRandomChannel.FieldCrops, field);
                var surface = random.Pick(_kit.crops, c => c.weight).surface;
                var seed = random.Next();
                _fields[field] = sown = (surface, seed, _context.Region(MapLandKind.Field, surface, seed, _angle));
            }

            return sown;
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
                var random = MapRandom.Stream(_seed, MapRandomChannel.FieldLeftovers, field);
                var surface = random.Pick(_kit.leftovers, c => c.weight).surface;
                _leftovers[field] = region = _context.Region(MapLandKind.Margin, surface, random.Next());
            }

            return region;
        }
    }
}
