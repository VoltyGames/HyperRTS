using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Generation;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;
using HyperRTS.Presentation.Environment.RowCrops;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>Bakes where row crops grow. Symmetry copies share the authored lattice and seed so both sides match.</summary>
    public static class MapCropPainter
    {
        private const float WaterClearance = 0.15f;
        private const int MaxFields = ushort.MaxValue - 1;

        // Roads claim their own cells, so none lies within a crop's margin past this distance.
        private const float RoadReach = 1.5f;

        /// <summary>Reads only plain data, so it may run on a worker thread.</summary>
        public static MapCropBake Paint(MapGeneration generation)
        {
            var (layout, shapes, heights) = (generation.Layout, generation.Shapes, generation.Heights);
            var clearance = generation.Placements.Clearance;
            var water = layout.Terrain.waterLevel;
            return Paint(generation.Land, layout.Symmetry, generation.Project.Climate, (point, edge, margin) =>
            {
                if (heights.Height(point) < water + WaterClearance || shapes.RiverEdgeDistance(point) < margin)
                {
                    return true;
                }

                var nearRoad = edge < margin + RoadReach && shapes.RoadEdgeDistance(point) < margin;
                return nearRoad || clearance.ReservedEdgeDistance(point, margin) < margin;
            });
        }

        /// <summary><paramref name="blocked"/> gets a cell centre, its distance to the field edge and the crop margin, and returns true to keep it bare.</summary>
        public static MapCropBake Paint(MapLand land, MapSymmetry symmetry, MapClimate climate,
            Func<Vector2, float, float, bool> blocked)
        {
            var bake = new MapCropBake(land.Bounds, land.Width, land.Height);
            var regionCrops = RegionCrops(land, climate, bake);
            AssignFields(land, symmetry, regionCrops, bake);
            Clear(land, bake, blocked);
            Tile(bake);
            return bake;
        }

        /// <summary>A copy's lattice: the region's rows in the authored sector, moved onto the copy.</summary>
        public static RowCropField Lattice(MapLandRegion region, MapTransform copy, RowCrop crop, int cropIndex)
        {
            var along = MapGeometry.Forward(region.RowAngle);
            var across = MapGeometry.Right(along);
            var hash = RowCropLattice.Hash(region.Seed, int.MinValue, int.MaxValue);
            var phaseAcross = RowCropLattice.Unit(RowCropLattice.Channel(hash, RowCropLattice.AcrossChannel));
            var phaseAlong = RowCropLattice.Unit(RowCropLattice.Channel(hash, RowCropLattice.AlongChannel));
            var origin = across * (phaseAcross * crop.rowSpacing) + along * (phaseAlong * crop.plantSpacing);
            return new RowCropField
            {
                origin = copy.Apply(origin),
                along = copy.Apply(along),
                across = copy.Apply(across),
                crop = cropIndex,
                seed = region.Seed,
            };
        }

        /// <summary>Per region, the index into the bake's crops of the crop its surface grows, or -1.</summary>
        private static int[] RegionCrops(MapLand land, MapClimate climate, MapCropBake bake)
        {
            var crops = new int[land.Regions.Count];
            var byKey = new Dictionary<string, int>();
            crops[0] = -1;
            for (var r = 1; r < crops.Length; r++)
            {
                var key = climate.Surface(land.Regions[r].Surface).rowCrop;
                if (string.IsNullOrEmpty(key))
                {
                    crops[r] = -1;
                    continue;
                }

                if (!byKey.TryGetValue(key, out var index))
                {
                    byKey[key] = index = bake.Crops.Count;
                    bake.Crops.Add(climate.rowCrops[climate.RowCropIndex(key)].crop);
                }

                crops[r] = index;
            }

            return crops;
        }

        /// <summary>Gives every crop cell the id of its (region, copy) field, adding fields as they are met.</summary>
        private static void AssignFields(MapLand land, MapSymmetry symmetry, int[] regionCrops, MapCropBake bake)
        {
            var fields = new Dictionary<(int Region, int Copy), int>();
            land.ForEach((x, z, center) =>
            {
                var region = land.RegionIndex(x, z);
                var crop = regionCrops[region];
                if (crop < 0)
                {
                    return;
                }

                symmetry.ToSector(center, out var copy);
                if (!fields.TryGetValue((region, copy), out var field))
                {
                    if (bake.Fields.Count == MaxFields)
                    {
                        throw new InvalidOperationException(
                            $"HyperRTS: a map can't sow more than {MaxFields} row crop fields; the field picture holds 16-bit ids.");
                    }

                    fields[(region, copy)] = field = bake.Fields.Count;
                    bake.Fields.Add(Lattice(land.Regions[region], symmetry.Copies[copy], bake.Crops[crop], crop));
                }

                bake.Ids[z * bake.Width + x] = RowCropFields.FieldId(field);
            });
        }

        /// <summary>Clears cells in their crop's edge margin or kept bare by <paramref name="blocked"/>, and counts the rest.</summary>
        private static void Clear(MapLand land, MapCropBake bake, Func<Vector2, float, float, bool> blocked)
        {
            var ids = bake.Ids;
            var edges = MapLandDistance.ToRegionEdge(bake.Width, bake.Height, (x, z) => ids[z * bake.Width + x]);
            var plants = 0d;
            for (var i = 0; i < ids.Length; i++)
            {
                if (ids[i] == RowCropFields.None)
                {
                    continue;
                }

                var crop = bake.Crops[bake.Fields[ids[i] - 1].crop];
                var edge = (edges[i] + 0.5f) * MapLand.CellSize;
                var center = land.Center(i % bake.Width, i / bake.Width);
                if (edge < crop.edgeMargin || blocked(center, edge, crop.edgeMargin))
                {
                    ids[i] = RowCropFields.None;
                    continue;
                }

                plants += MapLand.CellSize * MapLand.CellSize / crop.PlantArea;
            }

            bake.Plants = (long)Math.Round(plants);
        }

        private static void Tile(MapCropBake bake)
        {
            var cellsPerTile = Mathf.RoundToInt(RowCropFields.TileSize / MapLand.CellSize);
            var columns = (bake.Width + cellsPerTile - 1) / cellsPerTile;
            var rows = (bake.Height + cellsPerTile - 1) / cellsPerTile;
            var found = new SortedSet<int>();
            for (var tz = 0; tz < rows; tz++)
            {
                for (var tx = 0; tx < columns; tx++)
                {
                    found.Clear();
                    Collect(bake, tx * cellsPerTile, tz * cellsPerTile, cellsPerTile, found);
                    if (found.Count == 0)
                    {
                        continue;
                    }

                    bake.Tiles.Add(new RowCropTile { x = tx, z = tz, firstField = bake.TileFields.Count, fieldCount = found.Count });
                    bake.TileFields.AddRange(found);
                }
            }
        }

        private static void Collect(MapCropBake bake, int x0, int z0, int size, SortedSet<int> found)
        {
            var (x1, z1) = (Mathf.Min(x0 + size, bake.Width), Mathf.Min(z0 + size, bake.Height));
            for (var z = z0; z < z1; z++)
            {
                for (var x = x0; x < x1; x++)
                {
                    var id = bake.Id(x, z);
                    if (id != RowCropFields.None)
                    {
                        found.Add(id - 1);
                    }
                }
            }
        }
    }
}
