using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Build
{
    /// <summary>
    /// Everything a build makes before it touches a scene: the expanded layout, the shaped and painted ground, what
    /// stands on it, trees and grass. Trees inside the playable area become entity placements when the climate has
    /// a destructible version of them.
    /// </summary>
    public sealed class MapGeneration
    {
        private const float BaseBlend = 20f;
        private const float FootprintBlend = 5f;
        private const float TreeFootprint = 1.5f;

        public MapLayout Layout { get; private set; }
        public MapShapes Shapes { get; private set; }
        public MapHeights Heights { get; private set; }
        public MapPlacer Placer { get; private set; }
        public float[,,] Splat { get; private set; }
        public List<int[,]> Details { get; private set; }

        /// <summary>Trees left to the terrain (the scenery margin, and species without an entity version).</summary>
        public List<MapTree> TerrainTrees { get; } = new();

        public int TreeCount { get; private set; }

        /// <summary>The generated map, or null when the spec or assets have errors (they are in <paramref name="issues"/>).</summary>
        public static MapGeneration Run(MapSpec spec, MapProject project, MapIssues issues)
        {
            MapAssetCheck.Check(project, issues);
            var layout = MapExpander.Expand(spec, issues);
            if (issues.HasErrors)
            {
                return null;
            }

            var climate = project.Climate;
            var generation = new MapGeneration { Layout = layout, Shapes = new MapShapes(layout, project.Palette, issues) };
            generation.Heights = MapHeightShaper.Shape(layout, generation.Shapes);
            generation.Placer = MapPlacer.Place(layout, generation.Shapes, generation.Heights, project.Palette, climate, issues);
            generation.Level(project.Palette, issues);
            var resolution = MapTerrainWriter.AlphamapResolution(layout);
            generation.Splat = MapSplatPainter.Paint(layout, generation.Shapes, generation.Heights, climate, resolution, issues);
            if (issues.HasErrors)
            {
                return null;
            }

            var trees = MapTreePlanter.Plant(layout, generation.Shapes, generation.Heights, climate, generation.Placer, issues);
            generation.TreeCount = trees.Count;
            generation.Details = MapDetailPainter.Paint(layout, generation.Shapes, generation.Heights, climate, generation.Splat,
                generation.Placer.Clearance, MapTerrainWriter.DetailResolution(layout));
            generation.SplitTrees(trees, climate);
            return generation;
        }

        /// <summary>Levels every base and every footprint that asks for it, then keeps heights in the terrain's range.</summary>
        private void Level(MapPalette palette, MapIssues issues)
        {
            foreach (var spawn in Layout.Spawns)
            {
                Heights.Flatten(spawn.Position, palette.baseRadius, BaseBlend);
            }

            foreach (var placement in Placer.Placements)
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

        private void SplitTrees(List<MapTree> trees, MapClimate climate)
        {
            var entries = new Dictionary<int, MapPaletteEntry>();
            foreach (var tree in trees)
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

                Placer.Placements.Add(new MapPlacement(entry, tree.Position, tree.Yaw) { Scale = tree.Scale });
            }
        }
    }
}
