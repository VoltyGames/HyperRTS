using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning.War;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What every land pass works with: the layout and shaped ground, the land-use grid it claims, the placements it
    /// adds props to, the trees it plants, the decals it lays and the old war scars.
    /// </summary>
    public sealed class MapPlanContext
    {
        public MapPlanContext(MapLayout layout, MapShapes shapes, MapHeights heights, MapPalette palette,
            MapClimate climate, MapPlacements placements, MapIssues issues)
        {
            Layout = layout;
            Shapes = shapes;
            Heights = heights;
            Palette = palette;
            Climate = climate;
            Placements = placements;
            Issues = issues;
            Land = new MapLand(layout.TerrainBounds);
            Trees = new MapTreePlanting(layout.Symmetry, climate, new MapTreeSpacing(shapes, heights, placements.Clearance));
            WarScars = new MapWarScars(Noise(MapNoiseChannel.WarScars));
        }

        public MapLayout Layout { get; }
        public MapShapes Shapes { get; }

        /// <summary>The shaped ground; old craters are dug into it.</summary>
        public MapHeights Heights { get; }
        public MapPalette Palette { get; }
        public MapClimate Climate { get; }
        public MapPlacements Placements { get; }
        public MapIssues Issues { get; }
        public MapLand Land { get; }
        public MapTreePlanting Trees { get; }
        public MapWarScars WarScars { get; }

        /// <summary>Every ground decal laid so far, in map space (copies included).</summary>
        public List<MapDecal> Decals { get; } = new();

        /// <summary>Surface decals shown only after rain, in map space (copies included).</summary>
        public List<MapDecal> WetDecals { get; } = new();

        /// <summary>Props laid in the backdrop as terrain scenery, in map space (copies included).</summary>
        public List<MapScenery> Scenery { get; } = new();

        public int Seed => Layout.Spec.seed;

        /// <summary>The map's noise on one channel, symmetric across the sector copies.</summary>
        public MapNoise Noise(MapNoiseChannel channel) => new(Layout.Symmetry, Seed, channel);

        /// <summary>The random stream of one authored feature.</summary>
        public System.Random Random(MapRandomChannel channel, int index) => MapRandom.Stream(Seed, channel, index);

        /// <summary>A new land region painting a climate surface; land sown in rows gives their authored angle.</summary>
        public int Region(MapLandKind kind, string surface, int seed = 0, float? rowAngle = null) =>
            Land.Add(new MapLandRegion(kind, surface, seed, rowAngle));

        /// <summary>A new road region painting a climate surface with its road class's impact class.</summary>
        public int RoadRegion(string surface, string impactClass) => Land.Add(MapLandRegion.Road(surface, impactClass));
    }
}
