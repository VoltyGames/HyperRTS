using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Ground
{
    /// <summary>
    /// Grass, crops and small plants from the land use: each surface's plants cover the share it asks for, wild ground
    /// broken into clumps and clearings by noise (crops grow evenly), and weeds gathered in stands of their own; reeds
    /// stand in the shallows along the banks. Nothing grows on roads, tramlines, deep water or placed objects, and it
    /// thins in round them.
    /// </summary>
    public static class MapGrassPainter
    {
        private const float ReedDepth = 0.6f;
        private const float ReedReachIntoWater = 1.5f;

        // Share of wild ground in plant clumps; the rest is clearings where the terrain shows.
        private const float WildClumps = 0.6f;

        public const float PropGrassFalloff = 1.5f;

        /// <summary>Coverage per climate plant over the terrain, <paramref name="resolution"/> texels a side.</summary>
        public static MapGrassBake Paint(MapLayout layout, MapLand land, MapEdgeField edges, MapShapes shapes,
            MapHeights heights, MapClimate climate, MapClearance clearance, int resolution)
        {
            var area = layout.TerrainBounds;
            var bake = new MapGrassBake(area, resolution, climate.plants.Count);
            var plants = Plants(climate, edges);
            var reed = string.IsNullOrEmpty(climate.reedPlant) ? -1 : climate.PlantIndex(climate.reedPlant);
            var clumps = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.PlantClumps);
            var stands = StandNoise(layout, climate);
            var cell = area.size / resolution;
            var rowLines = new MapRowLines(layout.Symmetry, edges, cell.x);
            var water = layout.Terrain.waterLevel;
            for (var i = 0; i < resolution * resolution; i++)
            {
                var (x, z) = (i % resolution, i / resolution);
                var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), cell);
                var open = GrassNearProps(clearance, p);
                if (open <= 0f || shapes.RoadEdgeDistance(p) < 0.3f)
                {
                    continue;
                }

                var height = heights.Height(p);
                if (reed >= 0)
                {
                    bake.Set(reed, x, z, Reeds(shapes, climate, clumps, p, height, water) * open);
                }

                if (height < water + 0.15f)
                {
                    continue;
                }

                var region = edges.Sample(p).Region;
                var cover = Cover(IsField(land, region), clumps, p) * open * rowLines.Plants(region, p);
                foreach (var (plant, entry) in plants[region])
                {
                    var stand = entry.stands >= 1f ? 1f : stands[plant].Blotches(p, entry.standSize, entry.stands);
                    bake.Set(plant, x, z, entry.coverage * cover * stand);
                }
            }

            return bake;
        }

        /// <summary>Grass share near placed objects: none on a footprint, full past <see cref="PropGrassFalloff"/>.</summary>
        public static float GrassNearProps(MapClearance clearance, Vector2 point)
        {
            var edge = clearance.ReservedEdgeDistance(point, PropGrassFalloff);
            return float.IsPositiveInfinity(edge) ? 1f : MapGeometry.Smooth(edge / PropGrassFalloff);
        }

        /// <summary>Wild clumps at two scales with clearings between; crops are sown evenly and keep their cover.</summary>
        private static float Cover(bool sown, MapNoise clumps, Vector2 p)
        {
            if (sown)
            {
                return 0.92f + 0.08f * clumps.Sample(p, 9f);
            }

            return clumps.Blotches(p, 9f, WildClumps) * Mathf.Clamp01(0.75f + 0.5f * clumps.Sample(p, 2.5f));
        }

        /// <summary>One noise per climate plant, so each weed's stands lie apart from the others'.</summary>
        private static MapNoise[] StandNoise(MapLayout layout, MapClimate climate)
        {
            var noise = new MapNoise[climate.plants.Count];
            for (var i = 0; i < noise.Length; i++)
            {
                noise[i] = new MapNoise(layout.Symmetry, layout.Spec.seed + 7919 * i, MapNoiseChannel.PlantStands);
            }

            return noise;
        }

        /// <summary>Crops are sown evenly: a field's plants keep their cover instead of clumping like wild grass.</summary>
        private static bool IsField(MapLand land, int region) => region > 0 && land.Regions[region].Kind == MapLandKind.Field;

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

        /// <summary>Per region, the plants its surface grows, with each one's climate index.</summary>
        private static List<(int, MapPlantCoverage)>[] Plants(MapClimate climate, MapEdgeField edges)
        {
            var plants = new List<(int, MapPlantCoverage)>[edges.RegionCount];
            plants[MapLand.Unclaimed] = new List<(int, MapPlantCoverage)>();
            for (var i = 1; i < plants.Length; i++)
            {
                plants[i] = edges.Surface(i).plants.ConvertAll(entry => (climate.PlantIndex(entry.plant), entry));
            }

            return plants;
        }
    }
}
