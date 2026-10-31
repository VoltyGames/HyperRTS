using System.Collections.Generic;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Planning;
using HyperRTS.Editor.Maps.Planning.War;
using HyperRTS.Editor.Maps.Refine;
using HyperRTS.Editor.Maps.Report;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>A map generated from its project in three stages: <see cref="Lay"/>, <see cref="Plan"/> and <see cref="Run"/>.</summary>
    public sealed class MapGeneration
    {
        private const float BaseBlend = 20f;
        private const float FootprintBlend = 5f;
        private readonly MapIssues _issues;
        private MapPlanContext _plan;

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
        public MapHeights Heights => _plan.Heights;
        public MapPlacements Placements => _plan.Placements;
        public MapLand Land => _plan.Land;

        /// <summary>The land-use borders the ground texture, grass and ground tint painters share, so they end on one line.</summary>
        public MapEdgeField Edges { get; private set; }

        public List<MapTree> Trees => _plan.Trees.Planted;

        /// <summary>Ground decals laid on surfaces and under props, copies included.</summary>
        public List<MapDecal> Decals => _plan.Decals;

        /// <summary>Surface decals shown only after rain, copies included.</summary>
        public List<MapDecal> WetDecals => _plan.WetDecals;

        /// <summary>Where row crops grow, baked once the land is planned.</summary>
        public MapCropBake Crops { get; private set; }

        /// <summary>Old craters and burnt patches of the map's war age, copies included.</summary>
        public MapWarScars WarScars => _plan.WarScars;

        public float[,,] TextureWeights { get; private set; }
        public MapGrassBake Grass { get; private set; }

        /// <summary>Props in the backdrop, drawn on the terrain from their scenery models.</summary>
        public List<MapScenery> Scenery => _plan.Scenery;

        /// <summary>Trees left to the terrain once painted: the backdrop and species without an entity version.</summary>
        public List<MapTree> TerrainTrees { get; private set; } = new();

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
            Check(project, issues) ? CheckPlaced(PlanChecked(project, issues), issues) : null;

        /// <summary>The plan's checks that read prefabs, on the main thread after <see cref="PlanChecked"/>. Null on errors.</summary>
        public static MapGeneration CheckPlaced(MapGeneration generation, MapIssues issues)
        {
            generation?.Placements.CheckEntities(issues);
            return issues.HasErrors ? null : generation;
        }

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
            var heights = MapHeightShaper.Shape(Layout, Shapes);
            var placements = MapPropPlacer.Place(Layout, Shapes, palette, Project.Climate, _issues);
            _plan = new MapPlanContext(Layout, Shapes, heights, palette, Project.Climate, placements, _issues);
            MapLandPlanner.Plan(_plan);
            MapRoadChecks.Check(Layout, Shapes, palette.baseRadius, _issues);
            Crops = MapCropPainter.Paint(this);
            // Wet decals show on top of the dry ones after rain, so the cap holds for both together.
            var maxDecals = Project.Rules.maxDecals;
            var decals = Decals.Count + WetDecals.Count;
            if (decals > maxDecals)
            {
                _issues.Warn($"{decals} ground decals (wet ones included) are more than the rules' maxDecals {maxDecals}; lower surface decal densities.");
            }

            MapSurfaceProps.WarnBare(Land, Placements.All, Layout.Bounds, Project.Rules.propTargets, _issues);
        }

        private void Paint()
        {
            var climate = Project.Climate;
            Level();
            Edges = new MapEdgeField(Layout, Land, climate);
            TextureWeights = MapGroundTexturePainter.Paint(Layout, Edges, Heights, climate, Layout.GroundTextureResolution);
            WarScars.Burn(TextureWeights, Layout.TerrainBounds, climate);
            Grass = MapGrassPainter.Paint(Layout, Land, Edges, Shapes, Heights, climate, Placements.Clearance,
                Layout.GrassResolution);
            Grass.ThinByLayers(TextureWeights, climate.layers.ConvertAll(l => l.plants).ToArray());
            WarScars.Regrow(Grass, climate);
            TerrainTrees = _plan.Trees.PlaceEntities(Placements, Layout.Bounds);
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
    }
}
