using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Metrics;
using HyperRTS.Editor.Maps.Placement;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Resources;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Report
{
    /// <summary>
    /// The report's top-down overview: ground colours with hill shading, water, blocked ground in red, the counted
    /// attack paths, bases in their slot colours, resources, gameplay objects and crossings.
    /// </summary>
    public static class MapOverviewImage
    {
        private const int Size = 1024;
        private static readonly Color Water = new(0.18f, 0.32f, 0.5f);
        private static readonly Color Blocked = new(0.75f, 0.12f, 0.1f);
        private static readonly Color Resource = new(1f, 0.85f, 0.2f);
        private static readonly Color Crossing = new(0.3f, 0.95f, 1f);

        private static readonly Color[] PathColors =
        {
            new(1f, 1f, 1f), new(1f, 0.6f, 0.2f), new(0.6f, 1f, 0.3f), new(1f, 0.4f, 0.8f), new(0.5f, 0.8f, 1f),
            new(1f, 1f, 0.4f),
        };

        public static Texture2D Draw(MapLayout layout, MapHeights heights, float[,,] splat, MapClimate climate,
            MapNavGrid nav, MapShapes shapes, List<MapPlacement> placements, List<MapPath> paths)
        {
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            var pixels = new Color[Size * Size];
            for (var y = 0; y < Size; y++)
            {
                for (var x = 0; x < Size; x++)
                {
                    pixels[y * Size + x] = Ground(layout, heights, splat, climate, nav, Point(layout, x, y));
                }
            }

            for (var i = 0; i < paths.Count; i++)
            {
                DrawPath(pixels, layout, nav, paths[i], PathColors[i % PathColors.Length]);
            }

            foreach (var crossing in shapes.Crossings)
            {
                Disc(pixels, layout, crossing.Position, 3f, Crossing);
            }

            foreach (var placement in placements)
            {
                var resource = placement.Entry.prefab.GetComponentInChildren<ResourceNodeAuthoring>() != null;
                var color = placement.Owner > 0 ? PlayerSlot.Palette[(placement.Owner - 1) % PlayerSlot.Palette.Length]
                    : resource ? Resource : Color.white;
                if (placement.IsEntity)
                {
                    Disc(pixels, layout, placement.Position, Mathf.Max(1.5f, placement.Radius * 0.6f), color);
                }
            }

            image.SetPixels(pixels);
            image.Apply();
            return image;
        }

        private static Color Ground(MapLayout layout, MapHeights heights, float[,,] splat, MapClimate climate,
            MapNavGrid nav, Vector2 p)
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

            var h = heights.Height(p);
            var normal = new Vector3(heights.Height(p - Vector2.right) - heights.Height(p + Vector2.right), 2f,
                heights.Height(p - Vector2.up) - heights.Height(p + Vector2.up)).normalized;
            color *= Mathf.Clamp(0.55f + 0.6f * Vector3.Dot(normal, new Vector3(-0.5f, 0.75f, 0.45f).normalized), 0.4f, 1.15f);
            if (h < layout.Terrain.waterLevel)
            {
                color = Color.Lerp(color, Water, 0.75f);
            }

            var cell = nav.Cell(p);
            if (nav.Grid.InBounds(cell) && nav.Grid.Cells[nav.Grid.Index(cell)] == 0)
            {
                color = Color.Lerp(color, Blocked, 0.45f);
            }

            color.a = 1f;
            return color;
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
            var r = Mathf.CeilToInt(radius * scale);
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
