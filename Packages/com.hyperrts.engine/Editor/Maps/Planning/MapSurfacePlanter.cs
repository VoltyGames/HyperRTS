using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What grows or lies on each surface by itself: lone trees in meadows and fields, willows on banks, rocks on
    /// ravine rims, bales in rows on stubble the way a baler leaves them. Lands that plant their own trees (woods,
    /// belts, ravine floors) are skipped. Spots are chosen in the authored sector and copied.
    /// </summary>
    public sealed class MapSurfacePlanter : MapLandPass
    {
        private const int PropAttempts = 30;
        private const float RowSpacing = 16f;
        private const float AlongSpacing = 24f;
        private const float RowJitter = 2f;

        public MapSurfacePlanter(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var (index, cells) in SectorCells())
            {
                var region = Land.Regions[index];
                var surface = Climate.Surface(region.Surface);
                var hectares = cells.Count * MapLand.CellSize * MapLand.CellSize / 10000f;
                var random = Context.Random(MapRandomChannel.SurfaceGrowth, index);
                if (surface.trees.Count > 0 && !PlantsItself(region.Kind))
                {
                    Context.Trees.Scatter(surface.trees, hectares, surface.treeSpacing, random,
                        () => RandomSpot(cells, random), _ => true);
                }

                foreach (var props in surface.props)
                {
                    PlaceProps(region, props, cells, hectares, random);
                }
            }
        }

        /// <summary>Cells of the authored sector per region whose surface grows trees or props.</summary>
        private Dictionary<int, List<int>> SectorCells()
        {
            var cells = new Dictionary<int, List<int>>();
            var wanted = new bool[Land.Regions.Count];
            for (var i = 1; i < Land.Regions.Count; i++)
            {
                var surface = Climate.Surface(Land.Regions[i].Surface);
                wanted[i] = surface.trees.Count > 0 || surface.props.Count > 0;
            }

            Land.ForEach((x, z, p) =>
            {
                var region = Land.RegionIndex(x, z);
                if (!wanted[region] || !Symmetry.InSector(p))
                {
                    return;
                }

                if (!cells.TryGetValue(region, out var list))
                {
                    cells[region] = list = new List<int>();
                }

                list.Add(z * Land.Width + x);
            });
            return cells;
        }

        private void PlaceProps(MapLandRegion region, MapPropDensity props, List<int> cells, float hectares,
            System.Random random)
        {
            var entry = Context.Palette.Entry(props.kind);
            var wanted = Mathf.RoundToInt(props.perHectare * hectares);
            if (props.alongRows)
            {
                PlaceInRows(region, entry, cells, wanted, random);
                return;
            }

            var placed = 0;
            for (var attempt = 0; attempt < wanted * PropAttempts && placed < wanted; attempt++)
            {
                var spot = RandomSpot(cells, random);
                placed += TryPlace(entry, spot, random.Yaw(), random) ? 1 : 0;
            }
        }

        private void PlaceInRows(MapLandRegion region, MapPaletteEntry entry, List<int> cells, int wanted,
            System.Random random)
        {
            var forward = MapGeometry.Forward(region.RowAngle);
            var right = MapGeometry.Right(forward);
            var bounds = Bounds(cells);
            var reach = bounds.size.magnitude * 0.5f;
            var center = bounds.center;
            var phase = random.Value() * RowSpacing;
            var placed = 0;
            for (var across = -reach + phase; across < reach && placed < wanted; across += RowSpacing)
            {
                for (var along = -reach + random.Value() * AlongSpacing; along < reach && placed < wanted; along += AlongSpacing)
                {
                    var spot = center + right * across + forward * along + right * Jitter(random) + forward * Jitter(random);
                    if (Land.At(spot) == region && Symmetry.InSector(spot))
                    {
                        var yaw = region.RowAngle + 90f + Jitter(random) * 4f;
                        placed += TryPlace(entry, spot, yaw, random) ? 1 : 0;
                    }
                }
            }
        }

        private bool TryPlace(MapPaletteEntry entry, Vector2 spot, float yaw, System.Random random)
        {
            var scale = random.Range(entry.scale.x, entry.scale.y);
            var radius = entry.Radius * scale;
            var clearance = Context.Placements.Clearance;
            if (!clearance.IsOffRoad(spot, radius) || !clearance.IsFree(spot, radius))
            {
                return false;
            }

            if (!Context.Trees.Fits(spot, radius + 1f))
            {
                return false;
            }

            Context.Placements.AddEverywhere(entry, spot, yaw, scale);
            return true;
        }

        /// <summary>A random point inside a random cell of the list.</summary>
        private Vector2 RandomSpot(List<int> cells, System.Random random)
        {
            var cell = cells[random.Next(cells.Count)];
            var jitter = new Vector2(random.Value() - 0.5f, random.Value() - 0.5f);
            return Land.Center(cell % Land.Width, cell / Land.Width) + jitter * MapLand.CellSize;
        }

        private Rect Bounds(List<int> cells)
        {
            var min = new Vector2(float.MaxValue, float.MaxValue);
            var max = new Vector2(float.MinValue, float.MinValue);
            foreach (var cell in cells)
            {
                var center = Land.Center(cell % Land.Width, cell / Land.Width);
                min = Vector2.Min(min, center);
                max = Vector2.Max(max, center);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static float Jitter(System.Random random) => random.Signed() * RowJitter;

        private static bool PlantsItself(MapLandKind kind) =>
            kind is MapLandKind.Wood or MapLandKind.WoodEdge or MapLandKind.Belt or MapLandKind.Ravine;
    }
}
