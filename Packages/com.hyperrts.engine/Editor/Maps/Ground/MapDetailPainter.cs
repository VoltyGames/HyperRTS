using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Grass and small plants: each detail covers the share its layers ask for, broken into clumps by noise, and
    /// stays off roads, water and the footprints of placed objects.
    /// </summary>
    public static class MapDetailPainter
    {
        /// <summary>Coverage per detail prototype as Unity's detail layers want it: [z, x] in 0 to 255.</summary>
        public static List<int[,]> Paint(MapLayout layout, MapShapes shapes, MapHeights heights, MapClimate climate,
            float[,,] splat, MapClearance clearance, int resolution)
        {
            var coverage = Coverage(climate);
            var layers = new List<int[,]>();
            for (var d = 0; d < climate.details.Count; d++)
            {
                layers.Add(new int[resolution, resolution]);
            }

            var clumps = new MapNoise(layout.Symmetry, layout.Spec.seed, 4);
            var splatResolution = splat.GetLength(0);
            var area = layout.TerrainBounds;
            var cell = area.size / resolution;
            var water = layout.Terrain.waterLevel;
            for (var z = 0; z < resolution; z++)
            {
                for (var x = 0; x < resolution; x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), cell);
                    if (!Grows(p, shapes, heights, clearance, water))
                    {
                        continue;
                    }

                    var sx = Mathf.Min(splatResolution - 1, x * splatResolution / resolution);
                    var sz = Mathf.Min(splatResolution - 1, z * splatResolution / resolution);
                    var clump = Mathf.Clamp01(0.55f + 0.6f * clumps.Sample(p, 9f));
                    for (var d = 0; d < layers.Count; d++)
                    {
                        var share = 0f;
                        for (var l = 0; l < coverage.GetLength(0); l++)
                        {
                            share += splat[sz, sx, l] * coverage[l, d];
                        }

                        layers[d][z, x] = Mathf.RoundToInt(Mathf.Clamp01(share * clump) * 255f);
                    }
                }
            }

            return layers;
        }

        private static bool Grows(Vector2 p, MapShapes shapes, MapHeights heights, MapClearance clearance, float water)
        {
            if (shapes.RoadEdgeDistance(p) < 0.3f || heights.Height(p) < water + 0.15f)
            {
                return false;
            }

            return clearance.IsFree(p, 0.5f);
        }

        /// <summary>[layer, detail] coverage table from the climate's layers.</summary>
        private static float[,] Coverage(MapClimate climate)
        {
            var table = new float[climate.layers.Count, climate.details.Count];
            for (var l = 0; l < climate.layers.Count; l++)
            {
                foreach (var entry in climate.layers[l].details)
                {
                    var detail = climate.DetailIndex(entry.detail);
                    if (detail >= 0)
                    {
                        table[l, detail] = entry.coverage;
                    }
                }
            }

            return table;
        }
    }
}
