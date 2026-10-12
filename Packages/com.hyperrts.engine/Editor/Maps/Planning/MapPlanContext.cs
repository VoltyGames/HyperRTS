using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What every land pass works with: the layout and shaped ground, the land-use grid it claims, the placements it
    /// adds props to and the trees it plants.
    /// </summary>
    public sealed class MapPlanContext
    {
        public MapPlanContext(MapLayout layout, MapShapes shapes, MapHeights heights, MapPalette palette,
            MapClimate climate, MapPlacements placements, MapIssues issues)
        {
            Layout = layout;
            Shapes = shapes;
            Palette = palette;
            Climate = climate;
            Placements = placements;
            Issues = issues;
            Land = new MapLand(layout.TerrainBounds);
            Trees = new MapTreePlanting(layout.Symmetry, climate, new MapTreeSpacing(shapes, heights, placements.Clearance));
        }

        public MapLayout Layout { get; }
        public MapShapes Shapes { get; }
        public MapPalette Palette { get; }
        public MapClimate Climate { get; }
        public MapPlacements Placements { get; }
        public MapIssues Issues { get; }
        public MapLand Land { get; }
        public MapTreePlanting Trees { get; }

        /// <summary>Direction of the crop rows the terrain shader turns row layers to (degrees clockwise from north).</summary>
        public float RowAngle { get; set; }

        public int Seed => Layout.Spec.seed;

        /// <summary>The map's noise on one channel, symmetric across the sector copies.</summary>
        public MapNoise Noise(MapNoiseChannel channel) => new(Layout.Symmetry, Seed, channel);

        /// <summary>The random stream of one authored feature.</summary>
        public System.Random Random(MapRandomChannel channel, int index) => MapRandom.Stream(Seed, channel, index);

        /// <summary>A new land region painting a climate surface.</summary>
        public int Region(MapLandKind kind, string surface, int seed = 0, float rowAngle = 0f) =>
            Land.Add(new MapLandRegion(kind, surface, seed, rowAngle));
    }
}
