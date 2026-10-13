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

        public const float PropGrassFalloff = 1.5f;

        /// <summary>Coverage per detail prototype as Unity's detail layers want it: [z, x] in 0 to 255.</summary>
        public static List<int[,]> Paint(MapLayout layout, MapLand land, MapShapes shapes, MapHeights heights,
            MapClimate climate, MapClearance clearance, int resolution)
        {
            var layers = Layers(climate.details.Count, resolution);
            var edges = new MapEdgeField(layout, land, climate);
            var plants = Plants(land, climate, edges);
            var reed = string.IsNullOrEmpty(climate.reedDetail) ? -1 : climate.DetailIndex(climate.reedDetail);
            var clumps = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.PlantClumps);
            var area = layout.TerrainBounds;
            var cell = area.size / resolution;
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
                    layers[reed][z, x] = ToLayer(Reeds(shapes, climate, clumps, p, height, water) * open);
                }

                if (height < water + 0.15f)
                {
                    continue;
                }

                var growth = Growth(land, edges, plants, edges.Sample(p), out var sown);
                var cover = Cover(sown, clumps, p) * open;
                foreach (var (detail, coverage) in growth)
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

        /// <summary>Grass share near placed objects: none on a footprint, full past <see cref="PropGrassFalloff"/>.</summary>
        public static float GrassNearProps(MapClearance clearance, Vector2 point)
        {
            var edge = clearance.ReservedEdgeDistance(point, PropGrassFalloff);
            return float.IsPositiveInfinity(edge) ? 1f : MapGeometry.Smooth(edge / PropGrassFalloff);
        }

        /// <summary>Wild grass clumps at two scales; crops are sown evenly and keep their cover.</summary>
        private static float Cover(bool sown, MapNoise clumps, Vector2 p)
        {
            if (sown)
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

        /// <summary>
        /// The plants where the ground texture puts them: the region the border's middle assigns the point to, or that
        /// region's edge strip just inside it, which grows wild.
        /// </summary>
        private static List<(int Detail, float Coverage)> Growth(MapLand land, MapEdgeField edges,
            (List<(int, float)>[] Own, List<(int, float)>[] Strip) plants, MapEdgeSample edge, out bool sown)
        {
            var region = edge.Region;
            var strip = plants.Strip[region];
            var inStrip = strip != null && edge.Depth(region) < edges.Surface(region).edgeSurfaceWidth;
            sown = !inStrip && Sown(land, region);
            return inStrip ? strip : plants.Own[region];
        }

        /// <summary>
        /// Per region, the detail prototypes its surface grows and the share of ground each covers, and those of its
        /// edge strip (null without one). Unclaimed land grows nothing.
        /// </summary>
        private static (List<(int, float)>[] Own, List<(int, float)>[] Strip) Plants(MapLand land, MapClimate climate,
            MapEdgeField edges)
        {
            var own = new List<(int, float)>[land.Regions.Count];
            var strip = new List<(int, float)>[land.Regions.Count];
            own[0] = new List<(int, float)>();
            for (var i = 1; i < land.Regions.Count; i++)
            {
                var surface = edges.Surface(i);
                own[i] = Details(climate, surface);
                var hasStrip = !string.IsNullOrEmpty(surface.edgeSurface);
                strip[i] = hasStrip ? Details(climate, climate.Surface(surface.edgeSurface)) : null;
            }

            return (own, strip);
        }

        private static List<(int, float)> Details(MapClimate climate, MapClimateSurface surface) =>
            surface.details.ConvertAll(entry => (climate.DetailIndex(entry.detail), entry.coverage));
    }
}
