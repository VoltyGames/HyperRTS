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
    /// into clumps by noise at two scales (crops grow evenly); reeds stand in the shallows along the banks. Nothing grows on roads, in
    /// deep water or on placed objects, and it thins in round them.
    /// </summary>
    public static class MapDetailPainter
    {
        private const float ReedDepth = 0.6f;
        private const float ReedReachIntoWater = 1.5f;

        public const float PropFalloff = 1.5f;

        /// <summary>Coverage per detail prototype as Unity's detail layers want it: [z, x] in 0 to 255.</summary>
        public static List<int[,]> Paint(MapLayout layout, MapLand land, MapShapes shapes, MapHeights heights,
            MapClimate climate, MapClearance clearance, int resolution)
        {
            var layers = Layers(climate.details.Count, resolution);
            var plants = Plants(land, climate);
            var reed = string.IsNullOrEmpty(climate.reedDetail) ? -1 : climate.DetailIndex(climate.reedDetail);
            var clumps = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.PlantClumps);
            var edges = new MapEdgeJitter(layout);
            var area = layout.TerrainBounds;
            var cell = area.size / resolution;
            var water = layout.Terrain.waterLevel;
            for (var i = 0; i < resolution * resolution; i++)
            {
                var (x, z) = (i % resolution, i / resolution);
                var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), cell);
                var open = NearProps(clearance, p);
                if (shapes.RoadEdgeDistance(p) < 0.3f || open <= 0f)
                {
                    continue;
                }

                var height = heights.Height(p);
                if (reed >= 0)
                {
                    layers[reed][z, x] = ToLayer(Reeds(shapes, climate, clumps, p, height, water) * open);
                }

                if (height < water + 0.15f)
                {
                    continue;
                }

                var region = land.TryCell(p + edges.Offset(p), out var cx, out var cz) ? land.RegionIndex(cx, cz) : 0;
                var cover = Cover(land, region, clumps, p) * open;
                foreach (var (detail, coverage) in plants[region])
                {
                    layers[detail][z, x] = ToLayer(coverage * cover);
                }
            }

            return layers;
        }

        private static List<int[,]> Layers(int count, int resolution)
        {
            var layers = new List<int[,]>();
            for (var d = 0; d < count; d++)
            {
                layers.Add(new int[resolution, resolution]);
            }

            return layers;
        }

        /// <summary>Grass share near placed objects: none on a footprint, full past <see cref="PropFalloff"/>.</summary>
        public static float NearProps(MapClearance clearance, Vector2 point)
        {
            var edge = clearance.ReservedEdgeDistance(point, PropFalloff);
            return float.IsPositiveInfinity(edge) ? 1f : MapGeometry.Smooth(edge / PropFalloff);
        }

        /// <summary>Wild grass clumps at two scales; crops are sown evenly and keep their cover.</summary>
        private static float Cover(MapLand land, int region, MapNoise clumps, Vector2 p)
        {
            if (Sown(land, region))
            {
                return 0.92f + 0.08f * clumps.Sample(p, 9f);
            }

            return Mathf.Clamp01(0.55f + 0.45f * clumps.Sample(p, 9f) + 0.3f * clumps.Sample(p, 2.5f));
        }

        private static int ToLayer(float coverage) => Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);

        /// <summary>Crops are sown evenly: a field's plants keep their cover instead of clumping like wild grass.</summary>
        private static bool Sown(MapLand land, int region) => region > 0 && land.Regions[region].Kind == MapLandKind.Field;

        /// <summary>Reeds in stands from just inside the water to a few metres up the bank, only where it's shallow.</summary>
        private static float Reeds(MapShapes shapes, MapClimate climate, MapNoise clumps, Vector2 p, float height,
            float water)
        {
            var edge = shapes.RiverEdgeDistance(p);
            if (edge < -ReedReachIntoWater || edge > climate.reedWidth || height < water - ReedDepth)
            {
                return 0f;
            }

            return MapGeometry.Smooth((clumps.Sample(p + new Vector2(733f, 211f), 14f) + 0.1f) / 0.4f);
        }

        /// <summary>Per region, the detail prototypes its surface grows and the share of ground each covers.</summary>
        private static List<(int Detail, float Coverage)>[] Plants(MapLand land, MapClimate climate)
        {
            var plants = new List<(int, float)>[land.Regions.Count];
            plants[0] = new List<(int, float)>();
            for (var i = 1; i < land.Regions.Count; i++)
            {
                var surface = climate.Surface(land.Regions[i].Surface);
                plants[i] = surface.details.ConvertAll(entry => (climate.DetailIndex(entry.detail), entry.coverage));
            }

            return plants;
        }
    }
}
