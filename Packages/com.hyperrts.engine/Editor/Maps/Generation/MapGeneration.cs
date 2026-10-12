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
using UnityEngine;

namespace HyperRTS.Editor.Maps.Generation
{
    /// <summary>
    /// A map generated from its project before anything touches a scene, in three stages that each tool takes as far
    /// as it needs: <see cref="Lay"/> (checked, refined and expanded layout and its shapes), <see cref="Plan"/> (ground
    /// shape, placements, land use and trees) and <see cref="Run"/> (levelled and painted ground). Trees inside the
    /// playable area become entity placements when the climate has a destructible version of them.
    /// </summary>
    public sealed class MapGeneration
    {
        private const float BaseBlend = 20f;
        private const float FootprintBlend = 5f;
        private const float TreeFootprint = 1.5f;
        private readonly MapIssues _issues;

        private MapGeneration(MapProject project, MapLayout layout, MapIssues issues)
        {
            Project = project;
            Layout = layout;
            _issues = issues;
            Shapes = new MapShapes(layout, project.Palette, project.Climate);
        }

        public MapProject Project { get; }
        public MapLayout Layout { get; }
        public MapShapes Shapes { get; }
        public MapHeights Heights { get; private set; }
        public MapPlacements Placements { get; private set; }
        public MapLand Land { get; private set; }

        /// <summary>Direction of the crop rows (degrees clockwise from north) the terrain material turns row layers to.</summary>
        public float RowAngle { get; private set; }

        public List<MapTree> Trees { get; private set; }
        public float[,,] Splat { get; private set; }
        public List<int[,]> Details { get; private set; }

        /// <summary>Trees left to the terrain (the scenery margin, and species without an entity version).</summary>
        public List<MapTree> TerrainTrees { get; } = new();

        /// <summary>The checked layout and its shapes, all a map's views need; null when the spec or assets have errors.</summary>
        public static MapGeneration Lay(MapProject project, MapIssues issues)
        {
            MapAssetCheck.Check(project.Climate, project.Palette, issues);
            MapSpecCheck.Check(project.Spec, project.Climate, project.Palette, project.Rules, issues);
            if (issues.HasErrors)
            {
                return null;
            }

            MapRefiner.Refine(project.Spec, project.Climate);
            var layout = MapExpander.Expand(project.Spec, issues);
            return issues.HasErrors ? null : new MapGeneration(project, layout, issues);
        }

        /// <summary>The land planned but not painted: what a preview needs, in seconds. Null on errors.</summary>
        public static MapGeneration Plan(MapProject project, MapIssues issues)
        {
            var generation = Lay(project, issues);
            generation?.PlanLand();
            return issues.HasErrors ? null : generation;
        }

        /// <summary>Everything a build writes into the scene. Null on errors.</summary>
        public static MapGeneration Run(MapProject project, MapIssues issues)
        {
            var generation = Plan(project, issues);
            generation?.Paint();
            return generation;
        }

        private void PlanLand()
        {
            var palette = Project.Palette;
            Heights = MapHeightShaper.Shape(Layout, Shapes);
            Placements = MapPropPlacer.Place(Layout, Shapes, palette, Project.Climate, _issues);
            var context = new MapPlanContext(Layout, Shapes, Heights, palette, Project.Climate, Placements, _issues);
            MapLandPlanner.Plan(context);
            MapRoadChecks.Check(Layout, Shapes, palette.baseRadius, _issues);
            Land = context.Land;
            RowAngle = context.RowAngle;
            Trees = context.Trees.Planted;
        }

        private void Paint()
        {
            var climate = Project.Climate;
            Level();
            Splat = MapSplatPainter.Paint(Layout, Land, Heights, climate, Layout.SplatResolution);
            Details = MapDetailPainter.Paint(Layout, Land, Shapes, Heights, climate, Placements.Clearance,
                Layout.DetailResolution);
            SplitTrees(climate);
        }

        /// <summary>Levels every base and every footprint that asks for it, then keeps heights in the terrain's range.</summary>
        private void Level()
        {
            foreach (var spawn in Layout.Spawns)
            {
                Heights.Flatten(spawn.Position, Project.Palette.baseRadius, BaseBlend);
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
                _issues.Warn($"{clipped} terrain samples fell outside 0 to {Layout.Terrain.maxHeight} m and were clipped.");
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

                // Straight into the list: trees were spaced when planted and reserve no clearance now.
                Placements.All.Add(new MapPlacement(entry, tree.Position, tree.Yaw) { Scale = tree.Scale });
            }
        }
    }
}
