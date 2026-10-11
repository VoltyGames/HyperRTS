using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Grass, crops and small plants from the land use: each surface's plants cover the share it asks for, broken
    /// into clumps by noise at two scales; reeds stand in the shallows along the banks. Nothing grows on roads, in
    /// deep water or on placed objects.
    /// </summary>
    public static class MapDetailPainter
    {
        private const float ReedDepth = 0.6f;
        private const float ReedReachIntoWater = 1.5f;

        /// <summary>Coverage per detail prototype as Unity's detail layers want it: [z, x] in 0 to 255.</summary>
        public static List<int[,]> Paint(MapLayout layout, MapLand land, MapShapes shapes, MapHeights heights,
            MapClimate climate, MapClearance clearance, int resolution)
        {
            var layers = new List<int[,]>();
            for (var d = 0; d < climate.details.Count; d++)
            {
                layers.Add(new int[resolution, resolution]);
            }

            var plants = Plants(land, climate);
            var reed = string.IsNullOrEmpty(climate.reedDetail) ? -1 : climate.DetailIndex(climate.reedDetail);
            var clumps = new MapNoise(layout.Symmetry, layout.Spec.seed, 4);
            var jitter = new MapNoise(layout.Symmetry, layout.Spec.seed, 5);
            var area = layout.TerrainBounds;
            var cell = area.size / resolution;
            var water = layout.Terrain.waterLevel;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), cell);
                    if (shapes.RoadEdgeDistance(p) < 0.3f || !clearance.IsFree(p, 0.5f))
                    {
                        continue;
                    }

                    var height = heights.Height(p);
                    var clump = Mathf.Clamp01(0.55f + 0.45f * clumps.Sample(p, 9f) + 0.3f * clumps.Sample(p, 2.5f));
                    if (reed >= 0)
                    {
                        layers[reed][z, x] = Reeds(shapes, climate, clumps, p, height, water);
                    }

                    if (height < water + 0.15f)
                    {
                        continue;
                    }

                    var offset = new Vector2(jitter.Sample(p, 5f), jitter.Sample(p + new Vector2(97f, 41f), 5f)) * 1.2f;
                    var region = land.TryCell(p + offset, out var cx, out var cz) ? land.RegionIndex(cx, cz) : 0;
                    foreach (var (detail, coverage) in plants[region])
                    {
                        layers[detail][z, x] = Mathf.RoundToInt(Mathf.Clamp01(coverage * clump) * 255f);
                    }
                }
            }

            return layers;
        }

        /// <summary>Reeds in stands from just inside the water to a few metres up the bank, only where it's shallow.</summary>
        private static int Reeds(MapShapes shapes, MapClimate climate, MapNoise clumps, Vector2 p, float height,
            float water)
        {
            var edge = shapes.RiverEdgeDistance(p);
            if (edge < -ReedReachIntoWater || edge > climate.reedWidth || height < water - ReedDepth)
            {
                return 0;
            }

            var stands = MapGeometry.Smooth((clumps.Sample(p + new Vector2(733f, 211f), 14f) + 0.1f) / 0.4f);
            return Mathf.RoundToInt(stands * 255f);
        }

        /// <summary>Per region, the detail prototypes its surface grows and the share of ground each covers.</summary>
        private static List<(int Detail, float Coverage)>[] Plants(MapLand land, MapClimate climate)
        {
            var plants = new List<(int, float)>[land.Regions.Count];
            plants[0] = new List<(int, float)>();
            for (var i = 1; i < land.Regions.Count; i++)
            {
                plants[i] = new List<(int, float)>();
                var surface = climate.Surface(land.Regions[i].Surface);
                if (surface == null)
                {
                    continue;
                }

                foreach (var entry in surface.details)
                {
                    var detail = climate.DetailIndex(entry.detail);
                    if (detail >= 0)
                    {
                        plants[i].Add((detail, entry.coverage));
                    }
                }
            }

            return plants;
        }
    }
}
