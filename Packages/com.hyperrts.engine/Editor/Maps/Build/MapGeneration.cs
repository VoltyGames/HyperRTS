using System.Collections.Generic;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Refine;
using HyperRTS.Editor.Maps.Scenes;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Everything a build makes before it touches a scene: the refined and expanded layout, the shaped ground, the
    /// land use and what stands on it, then the painted ground, trees and plants. Trees inside the playable area
    /// become entity placements when the climate has a destructible version of them.
    /// </summary>
    public sealed class MapGeneration
    {
        private const float BaseBlend = 20f;
        private const float FootprintBlend = 5f;
        private const float TreeFootprint = 1.5f;

        public MapLayout Layout { get; private set; }
        public MapShapes Shapes { get; private set; }
        public MapHeights Heights { get; private set; }
        public MapPlacements Placements { get; private set; }
        public MapLand Land { get; private set; }

        /// <summary>Direction of the crop rows (degrees clockwise from north) the terrain material turns row layers to.</summary>
        public float RowAngle { get; private set; }

        public float[,,] Splat { get; private set; }
        public List<int[,]> Details { get; private set; }
        public List<MapTree> Trees { get; private set; }

        /// <summary>Trees left to the terrain (the scenery margin, and species without an entity version).</summary>
        public List<MapTree> TerrainTrees { get; } = new();

        /// <summary>The generated map, or null when the spec or assets have errors (they are in <paramref name="issues"/>).</summary>
        public static MapGeneration Run(MapSpec spec, MapProject project, MapIssues issues)
        {
            var generation = Plan(spec, project, issues);
            if (generation == null)
            {
                return null;
            }

            var climate = project.Climate;
            var layout = generation.Layout;
            generation.Level(project.Palette, issues);
            generation.Splat = MapSplatPainter.Paint(layout, generation.Land, generation.Heights, climate,
                MapTerrainWriter.AlphamapResolution(layout));
            generation.Details = MapDetailPainter.Paint(layout, generation.Land, generation.Shapes, generation.Heights,
                climate, generation.Placements.Clearance, MapTerrainWriter.DetailResolution(layout));
            generation.SplitTrees(climate);
            return generation;
        }

        /// <summary>
        /// The layout, ground shape, land use, placements and trees, without painting: what a preview needs, in
        /// seconds. Null on errors.
        /// </summary>
        public static MapGeneration Plan(MapSpec spec, MapProject project, MapIssues issues)
        {
            MapAssetCheck.Check(project.Climate, project.Palette, issues);
            MapSpecCheck.Check(spec, project.Climate, project.Palette, project.Rules, issues);
            if (issues.HasErrors)
            {
                return null;
            }

            MapRefiner.Refine(spec, project.Climate);
            var layout = MapExpander.Expand(spec, issues);
            if (issues.HasErrors)
            {
                return null;
            }

            var climate = project.Climate;
            var generation = new MapGeneration { Layout = layout };
            generation.Shapes = new MapShapes(layout, project.Palette, climate);
            generation.Heights = MapHeightShaper.Shape(layout, generation.Shapes);
            generation.Placements = MapPropPlacer.Place(layout, generation.Shapes, project.Palette, climate, issues);
            var context = new MapPlanContext(layout, generation.Shapes, generation.Heights, project.Palette, climate,
                generation.Placements, issues);
            MapLandPlanner.Plan(context);
            MapRoadChecks.Check(layout, generation.Shapes, project.Palette.baseRadius, issues);
            generation.Land = context.Land;
            generation.RowAngle = context.RowAngle;
            generation.Trees = context.Trees.Planted;
            return issues.HasErrors ? null : generation;
        }

        /// <summary>Levels every base and every footprint that asks for it, then keeps heights in the terrain's range.</summary>
        private void Level(MapPalette palette, MapIssues issues)
        {
            foreach (var spawn in Layout.Spawns)
            {
                Heights.Flatten(spawn.Position, palette.baseRadius, BaseBlend);
            }

            foreach (var placement in Placements.All)
            {
                if (placement.Entry.flatten && placement.Height == null)
                {
                    Heights.Flatten(placement.Position, placement.Radius + 1f, FootprintBlend);
                }
            }

            var clipped = Heights.Clamp(0f, Layout.Terrain.maxHeight);
            if (clipped > 0)
            {
                issues.Warn($"{clipped} terrain samples fell outside 0 to {Layout.Terrain.maxHeight} m and were clipped.");
            }
        }

        private void SplitTrees(MapClimate climate)
        {
            var entries = new Dictionary<int, MapPaletteEntry>();
            foreach (var tree in Trees)
            {
                var species = climate.trees[tree.Prototype];
                if (species.destructible == null || !Layout.Bounds.Contains(tree.Position))
                {
                    TerrainTrees.Add(tree);
                    continue;
                }

                if (!entries.TryGetValue(tree.Prototype, out var entry))
                {
                    entries[tree.Prototype] = entry = new MapPaletteEntry
                    {
                        key = species.key, prefab = species.destructible, size = Vector2.one * TreeFootprint,
                    };
                }

                Placements.All.Add(new MapPlacement(entry, tree.Position, tree.Yaw) { Scale = tree.Scale });
            }
        }
    }
}
