using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Heights;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Planning;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Ground
{
    /// <summary>The painters write the same heights, texture weights and grass on worker threads as in order.</summary>
    public class MapRowsTests
    {
        private const int Resolution = 128;

        [Test]
        public void ThreadedPainters_MatchTheSerialOnes()
        {
            var (serialHeights, serialWeights, serialGrass) = Paint(serial: true);
            var (heights, weights, grass) = Paint(serial: false);

            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    Assert.AreEqual(serialHeights[x, z], heights[x, z], $"height at {x}, {z}");
                }
            }

            for (var z = 0; z < Resolution; z++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    for (var l = 0; l < weights.LayerCount; l++)
                    {
                        Assert.AreEqual(serialWeights[x, z, l], weights[x, z, l], $"layer {l} at {x}, {z}");
                    }
                }
            }

            for (var p = 0; p < grass.Layers.Length; p++)
            {
                CollectionAssert.AreEqual(serialGrass.Layers[p], grass.Layers[p], $"plant {p}");
            }
        }

        private static (MapHeights, MapTextureWeights, MapGrassBake) Paint(bool serial)
        {
            MapRows.Serial = serial;
            try
            {
                var spec = MapFixture.Spec();
                spec.terrain.relief = 4f;
                spec.roads.Add(MapFixture.Road("Asphalt", new Vector2(-130f, -60f), new Vector2(-70f, -20f), new Vector2(-10f, -40f)));
                var climate = MapFixture.Climate();
                climate.slopeSurface = "Dirt";
                (climate.slopeStart, climate.slopeFull) = (8f, 20f);
                climate.plants.Add(MapFixture.Item<MapClimatePlant>("Grass"));
                climate.Surface("Meadow").plants.Add(new MapPlantCoverage { plant = "Grass", coverage = 0.6f });
                var context = MapFixture.Context(spec, climate, new MapIssues());
                var land = Land(context);
                var edges = new MapEdgeField(context.Layout, land, climate);
                var weights = MapGroundTexturePainter.Paint(context.Layout, edges, context.Heights, climate, Resolution);
                var grass = MapGrassPainter.Paint(context.Layout, land, edges, context.Shapes, context.Heights, climate,
                    context.Placements.Clearance, Resolution);
                grass.ThinByLayers(weights, new[] { 1f, 0.5f, 0.8f, 1f });
                return (context.Heights, weights, grass);
            }
            finally
            {
                MapRows.Serial = false;
            }
        }

        /// <summary>A dirt field south of a wavy line and meadow north of it, so texels blend across a border.</summary>
        private static MapLand Land(MapPlanContext context)
        {
            var land = new MapLand(context.Layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Dirt", 3));
            var meadow = land.Add(new MapLandRegion(MapLandKind.Open, "Meadow", 4));
            land.ClaimEach(land.Bounds, p => p.y < 20f * Mathf.Sin(p.x / 30f) ? field : meadow);
            return land;
        }
    }
}
