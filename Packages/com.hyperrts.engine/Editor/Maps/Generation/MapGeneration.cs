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
    /// <summary>A map generated from its project in three stages: <see cref="Lay"/>, <see cref="Plan"/> and <see cref="Run"/>.</summary>
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

        public List<MapTree> Trees { get; private set; }

        /// <summary>Ground decals laid on surfaces and under props, copies included.</summary>
        public List<MapDecal> Decals { get; private set; }

        /// <summary>Where row crops grow, baked once the land is planned.</summary>
        public MapCropBake Crops { get; private set; }

        public float[,,] Splat { get; private set; }
        public List<int[,]> Details { get; private set; }

        /// <summary>Trees left to the terrain: the backdrop and species without an entity version.</summary>
        public List<MapTree> TerrainTrees { get; } = new();

        /// <summary>Asset and spec checks. They read prefabs, so they run on the main thread.</summary>
        public static bool Check(MapProject project, MapIssues issues)
        {
            MapAssetCheck.Check(project.Climate, project.Palette, issues);
            MapSpecCheck.Check(project.Spec, project.Climate, project.Palette, project.Rules, issues);
            return !issues.HasErrors;
        }

        /// <summary>The checked layout and its shapes, or null on errors.</summary>
        public static MapGeneration Lay(MapProject project, MapIssues issues) =>
            Check(project, issues) ? LayChecked(project, issues) : null;

        /// <summary>The land planned but not painted, for previews. Null on errors.</summary>
        public static MapGeneration Plan(MapProject project, MapIssues issues) =>
            Check(project, issues) ? PlanChecked(project, issues) : null;

        /// <summary><see cref="Plan"/> after <see cref="Check"/>. Touches no Unity object, so it may run on a worker thread.</summary>
        public static MapGeneration PlanChecked(MapProject project, MapIssues issues)
        {
            var generation = LayChecked(project, issues);
            generation?.PlanLand();
            return issues.HasErrors ? null : generation;
        }

        private static MapGeneration LayChecked(MapProject project, MapIssues issues)
        {
            MapRefiner.Refine(project.Spec, project.Climate);
            var layout = MapExpander.Expand(project.Spec, issues);
            return issues.HasErrors ? null : new MapGeneration(project, layout, issues);
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
            Trees = context.Trees.Planted;
            Decals = context.Decals;
            Crops = MapCropPainter.Paint(this);
            var maxDecals = Project.Rules.maxDecals;
            if (Decals.Count > maxDecals)
            {
                _issues.Warn($"{Decals.Count} ground decals are more than the rules' maxDecals {maxDecals}; lower surface decal densities.");
            }
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
            var entries = new Dictionary<(int, int), MapPaletteEntry>();
            foreach (var tree in Trees)
            {
                var species = climate.trees[tree.Species];
                if (!species.HasEntities || !Layout.Bounds.Contains(tree.Position))
                {
                    TerrainTrees.Add(tree);
                    continue;
                }

                if (!entries.TryGetValue((tree.Species, tree.Variant), out var entry))
                {
                    entries[(tree.Species, tree.Variant)] = entry = new MapPaletteEntry
                    {
                        key = species.key, prefab = species.destructibles[tree.Variant], size = Vector2.one * TreeFootprint,
                    };
                }

                // Trees were spaced when planted, so they skip the clearance check.
                Placements.All.Add(new MapPlacement(entry, tree.Position, tree.Yaw, 1f) { Scale = tree.Scale });
            }
        }
    }
}
