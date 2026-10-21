using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// What grows or lies on each surface by itself (lone trees, bank willows, rim rocks, bales in rows, scrub);
    /// lands that plant their own trees are skipped. Spots are chosen in the authored sector and copied. Props falling
    /// in the backdrop become scenery when they have a scenery model.
    /// </summary>
    public sealed class MapSurfacePlanter : MapLandStep
    {
        private const int PropAttempts = 30;
        private const float RowSpacing = 16f;
        private const float AlongSpacing = 24f;
        private const float RowJitter = 2f;

        // Metres between a prop's footprint and the centre of the nearest tree, so props fit between wood trees.
        private const float TreeClearance = 0.5f;
        private float[] _edges;

        public MapSurfacePlanter(MapPlanContext context) : base(context)
        {
        }

        public override void Plan()
        {
            foreach (var (index, cells) in SectorCells(Grows, playableOnly: false))
            {
                var region = Land.Regions[index];
                var surface = Climate.Surface(region.Surface);
                var hectares = Hectares(cells.Count);
                var random = Context.Random(MapRandomChannel.SurfaceGrowth, index);
                if (!PlantsItself(region.Kind))
                {
                    PlantTrees(surface, cells, random);
                }

                foreach (var props in surface.props)
                {
                    PlaceProps(region, props, cells, hectares, random);
                }
            }
        }

        /// <summary>Edge-limited trees count per hectare of their band, so narrow bands still get their share.</summary>
        private void PlantTrees(MapClimateSurface surface, List<int> cells, System.Random random)
        {
            foreach (var density in surface.trees)
            {
                var reach = density.maxEdgeDistance;
                var limited = reach > 0f;
                var band = limited ? cells.FindAll(cell => EdgeDistance(cell) <= reach) : cells;
                if (band.Count == 0)
                {
                    continue;
                }

                Func<Vector2, bool> inside = limited ? p => EdgeDistance(p) <= reach : _ => true;
                Context.Trees.Scatter(density, Hectares(band.Count), surface.treeSpacing, random,
                    () => RandomSpot(band, random), inside);
            }
        }

        private float EdgeDistance(int cell)
        {
            _edges ??= MapLandDistance.ToRegionEdge(Land.Width, Land.Height, Land.RegionIndex);
            return _edges[cell] * MapLand.CellSize;
        }

        private float EdgeDistance(Vector2 point) =>
            Land.TryCell(point, out var x, out var z) ? EdgeDistance(z * Land.Width + x) : float.PositiveInfinity;

        private void PlaceProps(MapLandRegion region, MapPropDensity props, List<int> cells, float hectares,
            System.Random random)
        {
            var entry = Context.Palette.Entry(props.kind);
            var wanted = random.Count(props.perHectare * hectares);
            if (props.alongRows)
            {
                PlaceInRows(region, entry, cells, wanted, random);
                return;
            }

            var placed = 0;
            for (var attempt = 0; attempt < wanted * PropAttempts && placed < wanted; attempt++)
            {
                var spot = RandomSpot(cells, random);
                placed += TryPlace(entry, spot, random.Signed() * entry.yawJitter, random) ? 1 : 0;
            }
        }

        private void PlaceInRows(MapLandRegion region, MapProp entry, List<int> cells, int wanted,
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

        private bool TryPlace(MapProp entry, Vector2 spot, float yaw, System.Random random)
        {
            var scale = random.Range(entry.scale.x, entry.scale.y);
            var radius = entry.Radius * scale;
            if (!Context.Layout.Bounds.Contains(spot))
            {
                return entry.backdropModel != null && TryPlaceScenery(entry.backdropModel, spot, yaw, scale, radius);
            }

            var clearance = Context.Placements.Clearance;
            if (!clearance.IsOffRoad(spot, radius) || !clearance.IsFree(spot, radius))
            {
                return false;
            }

            if (!Context.Trees.Fits(spot, radius + TreeClearance))
            {
                return false;
            }

            Context.Placements.AddEverywhere(entry, spot, yaw, scale, 1f);
            return true;
        }

        /// <summary>Scenery is no obstacle, so it only keeps off roads, rivers, trees and other scenery.</summary>
        private bool TryPlaceScenery(GameObject model, Vector2 spot, float yaw, float scale, float radius)
        {
            var shapes = Context.Shapes;
            if (shapes.RoadEdgeDistance(spot) < radius || shapes.RiverEdgeDistance(spot) < radius)
            {
                return false;
            }

            if (!Context.Trees.Fits(spot, radius + TreeClearance))
            {
                return false;
            }

            foreach (var other in Context.Scenery)
            {
                if (Vector2.Distance(other.Position, spot) < other.Radius + radius)
                {
                    return false;
                }
            }

            foreach (var copy in Symmetry.Copies)
            {
                Context.Scenery.Add(new MapScenery(model, copy.Apply(spot), copy.ApplyYaw(yaw), scale, radius));
            }

            return true;
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

        private static bool Grows(MapClimateSurface surface) => surface.trees.Count > 0 || surface.props.Count > 0;

        private static bool PlantsItself(MapLandKind kind) =>
            kind is MapLandKind.Wood or MapLandKind.WoodEdge or MapLandKind.Belt or MapLandKind.Ravine;
    }
}
