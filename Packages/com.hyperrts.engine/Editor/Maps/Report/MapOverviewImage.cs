using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Nav;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// The report's top-down overview: ground colours with hill shading, water, blocked ground in red, the counted
    /// attack paths, bases in their slot colours, resources, gameplay objects and crossings. A preview has no nav grid
    /// or paths yet and draws the land use with its trees instead.
    /// </summary>
    public static class MapOverviewImage
    {
        private const int Size = 1024;
        private static readonly Color Water = new(0.18f, 0.32f, 0.5f);
        private static readonly Color BlockedColor = new(0.75f, 0.12f, 0.1f);
        private static readonly Color Resource = new(1f, 0.85f, 0.2f);
        private static readonly Color Crossing = new(0.3f, 0.95f, 1f);
        private static readonly Color TreeColor = new(0.08f, 0.2f, 0.06f);
        private static readonly Color SceneryColor = new(0.85f, 0.8f, 0.7f);

        private static readonly Color[] PathColors =
        {
            new(1f, 1f, 1f), new(1f, 0.6f, 0.2f), new(0.6f, 1f, 0.3f), new(1f, 0.4f, 0.8f), new(0.5f, 0.8f, 1f),
            new(1f, 1f, 0.4f),
        };

        /// <summary>The built map: painted ground, blocked cells and the measured attack paths.</summary>
        public static Texture2D Draw(MapGeneration generation, MapNavGrid nav, List<MapPath> paths)
        {
            var (layout, splat, climate) = (generation.Layout, generation.Splat, generation.Project.Climate);
            var pixels = Shade(layout, generation.Heights, p => Blocked(nav, p, SplatColor(layout, splat, climate, p)));
            for (var i = 0; i < paths.Count; i++)
            {
                DrawPath(pixels, layout, nav, paths[i], PathColors[i % PathColors.Length]);
            }

            return Finish(pixels, layout, generation.Shapes, generation.Placements.All, false);
        }

        /// <summary>A preview: land-use colours, trees as dark dots, scenery and gameplay objects.</summary>
        public static Texture2D DrawLand(MapGeneration generation)
        {
            var (layout, land) = (generation.Layout, generation.Land);
            var colors = LandColors(land, generation.Project.Climate);
            var pixels = Shade(layout, generation.Heights, p => land.TryCell(p, out var x, out var z) ? colors[land.RegionIndex(x, z)] : Color.black);
            foreach (var tree in generation.Trees)
            {
                Disc(pixels, layout, tree.Position, 1.4f * tree.Scale, TreeColor);
            }

            return Finish(pixels, layout, generation.Shapes, generation.Placements.All, true);
        }

        private static Color[] Shade(MapLayout layout, MapHeights heights, System.Func<Vector2, Color> ground)
        {
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    var p = Point(layout, x, y);
                    pixels[y * Size + x] = Relief(layout, heights, p, ground(p));
                }
            }

            return pixels;
        }

        private static Texture2D Finish(Color[] pixels, MapLayout layout, MapShapes shapes, List<MapPlacement> placements,
            bool scenery)
        {
            foreach (var crossing in shapes.Crossings)
            {
                Disc(pixels, layout, crossing.Position, 3f, Crossing);
            }

            foreach (var placement in placements)
            {
                if (!placement.IsEntity && !scenery)
                {
                    continue;
                }

                var color = placement.Owner > 0 ? PlayerSlot.Palette[(placement.Owner - 1) % PlayerSlot.Palette.Length]
                    : placement.Entry.IsResource ? Resource : placement.Entry.IsGameplay ? Color.white : SceneryColor;
                Disc(pixels, layout, placement.Position, Mathf.Max(0.8f, placement.Radius * 0.6f), color);
            }

            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            image.SetPixels(pixels);
            image.Apply();
            return image;
        }

        private static Color SplatColor(MapLayout layout, float[,,] splat, MapClimate climate, Vector2 p)
        {
            var resolution = splat.GetLength(0);
            var area = layout.TerrainBounds;
            var sx = Mathf.Clamp((int)((p.x - area.xMin) / area.width * resolution), 0, resolution - 1);
            var sz = Mathf.Clamp((int)((p.y - area.yMin) / area.height * resolution), 0, resolution - 1);
            var color = Color.black;
            for (var l = 0; l < climate.layers.Count; l++)
            {
                color += climate.layers[l].mapColor * splat[sz, sx, l];
            }

            return color;
        }

        /// <summary>Each land region in the overview colour of the layer its surface paints.</summary>
        private static Color[] LandColors(MapLand land, MapClimate climate)
        {
            var colors = new Color[land.Regions.Count];
            for (var i = 1; i < colors.Length; i++)
            {
                colors[i] = climate.layers[climate.SurfaceLayer(land.Regions[i].Surface)].mapColor;
            }

            return colors;
        }

        /// <summary>Hill shading and water over a ground colour.</summary>
        private static Color Relief(MapLayout layout, MapHeights heights, Vector2 p, Color color)
        {
            var normal = new Vector3(heights.Height(p - Vector2.right) - heights.Height(p + Vector2.right), 2f,
                heights.Height(p - Vector2.up) - heights.Height(p + Vector2.up)).normalized;
            color *= Mathf.Clamp(0.55f + 0.6f * Vector3.Dot(normal, new Vector3(-0.5f, 0.75f, 0.45f).normalized), 0.4f, 1.15f);
            if (heights.Height(p) < layout.Terrain.waterLevel)
            {
                color = Color.Lerp(color, Water, 0.75f);
            }

            color.a = 1f;
            return color;
        }

        private static Color Blocked(MapNavGrid nav, Vector2 p, Color color)
        {
            var cell = nav.Cell(p);
            var blocked = nav.Grid.InBounds(cell) && nav.Grid.Cells[nav.Grid.Index(cell)] == (byte)NavSurface.Blocked;
            return blocked ? Color.Lerp(color, BlockedColor, 0.45f) : color;
        }

        private static void DrawPath(Color[] pixels, MapLayout layout, MapNavGrid nav, MapPath path, Color color)
        {
            foreach (var cell in path.Cells)
            {
                var center = nav.Center(cell);
                Disc(pixels, layout, new Vector2(center.x, center.z), 1.2f, color);
            }
        }

        private static void Disc(Color[] pixels, MapLayout layout, Vector2 center, float radius, Color color)
        {
            var scale = Size / layout.Size.x;
            var pixel = (center - layout.Bounds.min) * scale;
            var r = Mathf.Max(1, Mathf.CeilToInt(radius * scale));
            for (var y = -r; y <= r; y++)
            {
                for (var x = -r; x <= r; x++)
                {
                    var px = (int)pixel.x + x;
                    var py = (int)pixel.y + y;
                    if (x * x + y * y <= r * r && px >= 0 && py >= 0 && px < Size && py < Size)
                    {
                        pixels[py * Size + px] = color;
                    }
                }
            }
        }

        private static Vector2 Point(MapLayout layout, int x, int y) =>
            layout.Bounds.min + new Vector2((x + 0.5f) / Size * layout.Size.x, (y + 0.5f) / Size * layout.Size.y);
    }
}
