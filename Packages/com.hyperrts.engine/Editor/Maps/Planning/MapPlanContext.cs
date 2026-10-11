using System.Collections.Generic;
using System.Linq;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What every land planner works with: the layout and shaped ground, the land-use grid it claims, the placer it
    /// adds props to and the trees it plants. Choices are made in the authored sector and copied by the symmetry.
    /// </summary>
    public sealed class MapPlanContext
    {
        public MapPlanContext(MapLayout layout, MapShapes shapes, MapHeights heights, MapPalette palette,
            MapClimate climate, MapPlacer placer, MapIssues issues)
        {
            Layout = layout;
            Shapes = shapes;
            Heights = heights;
            Palette = palette;
            Climate = climate;
            Placer = placer;
            Issues = issues;
            Land = new MapLand(layout.TerrainBounds);
            Spacing = new MapTreeSpacing(shapes, heights, placer.Clearance);
        }

        public MapLayout Layout { get; }
        public MapShapes Shapes { get; }
        public MapHeights Heights { get; }
        public MapPalette Palette { get; }
        public MapClimate Climate { get; }
        public MapPlacer Placer { get; }
        public MapIssues Issues { get; }
        public MapLand Land { get; }

        /// <summary>Every tree planted so far, in map space (copies included).</summary>
        public List<MapTree> Trees { get; } = new();

        public MapTreeSpacing Spacing { get; }

        /// <summary>Direction of the crop rows the terrain shader turns row layers to (degrees clockwise from north).</summary>
        public float RowAngle { get; set; }

        public int Seed => Layout.Spec.seed;

        /// <summary>A land region painting a climate surface; an unknown surface is an error.</summary>
        public int Region(MapLandKind kind, string surface, string source, int seed = 0, float rowAngle = 0f)
        {
            if (Climate.Surface(surface) == null)
            {
                Issues.Error($"{source}: surface '{surface}' is not in climate '{Climate.name}'.");
            }

            return Land.Add(new MapLandRegion(kind, surface, seed, rowAngle));
        }

        /// <summary>A climate tree's index; an unknown key is an error and returns -1.</summary>
        public int Tree(string key, string source)
        {
            var index = Climate.TreeIndex(key);
            if (index < 0)
            {
                Issues.Error($"{source}: tree '{key}' is not in climate '{Climate.name}'.");
            }

            return index;
        }

        /// <summary>Plants a tree chosen at an authored-sector point in every copy of the sector.</summary>
        public void PlantCopies(int tree, Vector2 sectorPoint, float scale, float yaw)
        {
            foreach (var copy in Layout.Symmetry.Copies)
            {
                var placed = copy.Apply(sectorPoint);
                Spacing.Add(placed);
                Trees.Add(new MapTree(tree, placed, scale, copy.ApplyYaw(yaw)));
            }
        }

        /// <summary>A random scale inside a climate tree's range.</summary>
        public float TreeScale(int tree, System.Random random)
        {
            var range = Climate.trees[tree].scale;
            return Mathf.Lerp(range.x, range.y, (float)random.NextDouble());
        }

        /// <summary>Picks one entry of a weighted mix.</summary>
        public static T Pick<T>(IReadOnlyList<T> items, System.Func<T, float> weight, System.Random random)
        {
            var total = items.Sum(weight);
            var roll = (float)random.NextDouble() * total;
            foreach (var item in items)
            {
                roll -= weight(item);
                if (roll <= 0f)
                {
                    return item;
                }
            }

            return items[items.Count - 1];
        }
    }
}
