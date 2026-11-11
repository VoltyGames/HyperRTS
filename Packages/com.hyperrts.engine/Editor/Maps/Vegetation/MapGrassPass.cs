using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Placement;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Vegetation
{
    /// <summary>Paints one grass texel at a time: reeds by the water, then each plant its land use grows.</summary>
    public sealed class MapGrassPass
    {
        private const float ReedDepth = 0.6f;
        private const float ReedReachIntoWater = 1.5f;

        // Share of wild ground in plant clumps; the rest is clearings where the terrain shows.
        private const float WildClumps = 0.6f;

        private readonly MapLand _land;
        private readonly MapEdgeField _edges;
        private readonly MapShapes _shapes;
        private readonly MapHeights _heights;
        private readonly MapClearance _clearance;
        private readonly MapGrassBake _bake;
        private readonly MapClimate _climate;
        private readonly Rect _area;
        private readonly Vector2 _cell;
        private readonly int _reed;
        private readonly float _water;
        private readonly MapNoise _clumps;
        private readonly MapNoise[] _stands;
        private readonly MapRowLines _rowLines;
        private readonly List<(int, MapPlantCoverage)>[] _plants;

        public MapGrassPass(MapLand land, MapEdgeField edges, MapShapes shapes, MapHeights heights, MapClearance clearance,
            MapGrassBake bake)
        {
            (_land, _edges, _shapes, _heights, _clearance, _bake) = (land, edges, shapes, heights, clearance, bake);
            var layout = shapes.Layout;
            _climate = shapes.Climate;
            _area = layout.TerrainBounds;
            _cell = _area.size / bake.Resolution;
            _plants = Plants(_climate, edges);
            _reed = string.IsNullOrEmpty(_climate.reedPlant) ? -1 : _climate.PlantIndex(_climate.reedPlant);
            _clumps = new MapNoise(layout.Symmetry, layout.Spec.seed, MapNoiseChannel.PlantClumps);
            // One noise per climate plant, so each weed's stands lie apart from the others'.
            _stands = MapNoise.CreateMany(layout, MapNoiseChannel.PlantStands, _climate.plants.Count);
            _rowLines = new MapRowLines(layout.Symmetry, edges, _cell.x);
            _water = layout.Terrain.waterLevel;
        }

        public void PaintCell(int x, int z)
        {
            var p = _area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), _cell);
            var open = MapGrassPainter.GrassNearProps(_clearance, p);
            if (open <= 0f || _shapes.RoadEdgeDistance(p) < 0.3f)
            {
                return;
            }

            var height = _heights.Height(p);
            if (_reed >= 0)
            {
                _bake.Set(_reed, x, z, Reeds(p, height) * open);
            }

            if (height < _water + 0.15f)
            {
                return;
            }

            var region = _edges.Sample(p).Region;
            var coverage = Coverage(IsField(region), p) * open * _rowLines.Plants(region, p);
            foreach (var (plant, entry) in _plants[region])
            {
                var stand = entry.stands >= 1f ? 1f : _stands[plant].Blotches(p, entry.standSize, entry.stands);
                _bake.Set(plant, x, z, entry.coverage * coverage * stand);
            }
        }

        /// <summary>Wild clumps at two scales with clearings between; crops are sown evenly and keep their cover.</summary>
        private float Coverage(bool sown, Vector2 p)
        {
            if (sown)
            {
                return 0.92f + 0.08f * _clumps.Sample(p, 9f);
            }

            return _clumps.Blotches(p, 9f, WildClumps) * Mathf.Clamp01(0.75f + 0.5f * _clumps.Sample(p, 2.5f));
        }

        /// <summary>Crops are sown evenly: a field's plants keep their cover instead of clumping like wild grass.</summary>
        private bool IsField(int region) => region > 0 && _land.Regions[region].Kind == MapLandKind.Field;

        /// <summary>Reeds in stands from just inside the water to a few metres up the bank, only where it's shallow.</summary>
        private float Reeds(Vector2 p, float height)
        {
            var edge = _shapes.WaterEdgeDistance(p);
            if (edge < -ReedReachIntoWater || edge > _climate.reedWidth || height < _water - ReedDepth)
            {
                return 0f;
            }

            return MapGeometry.Smooth((_clumps.Sample(p + new Vector2(733f, 211f), 14f) + 0.1f) / 0.4f);
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
