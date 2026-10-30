using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>Lines along a sown field's rows in every copy: tramlines bare of crops, and lighter, thinner harvest swaths.</summary>
    public class MapRowLineTests : TemplateFixture
    {
        private const float Spacing = 12f;
        private const float Width = 0.5f;

        [Test]
        public void CropGaps_LieOnThePaintedWheelLines_InBothCopies()
        {
            var crop = Track(ScriptableObject.CreateInstance<RowCrop>());
            var climate = Climate(crop);
            var layout = MapFixture.Layout(MapFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Sown", 7, 30f));
            land.ClaimWhere(land.Bounds, field, p => InField(p, 0f) || InField(-p, 0f));
            var bake = MapCropPainter.Paint(land, layout.Symmetry, climate, (_, _, _) => false);
            var lines = new MapRowLines(layout.Symmetry, new MapEdgeField(layout, land, climate), 0.02f);

            Assert.AreEqual(2, bake.Fields.Count, "one field per copy");
            for (var f = 0; f < bake.Fields.Count; f++)
            {
                var lattice = bake.Fields[f];
                var (gaps, plants) = (0, 0);
                for (var row = -160; row < 160; row++)
                {
                    for (var plant = -320; plant < 320; plant++)
                    {
                        var position = RowCropSpots.Position(lattice, row, plant, crop);
                        var across = Vector2.Dot(position - lattice.origin, lattice.across);
                        var margin = Mathf.Abs(lattice.tramlines.Distance(across) - Width * 0.5f);
                        var inside = InField(position, 2f) || InField(-position, 2f);
                        if (!inside || margin < 0.02f || !Sown(bake, land, position, f))
                        {
                            continue;
                        }

                        var gap = RowCropSpots.OnTramline(lattice, row, plant, crop);
                        Assert.AreEqual(gap, lines.Tramline(field, position) > 0.5f, $"plant at {position}");
                        gaps += gap ? 1 : 0;
                        plants++;
                    }
                }

                Assert.That(gaps / (float)plants, Is.InRange(0.5f * Width / Spacing, 4f * Width / Spacing),
                    "two wheel lines in every tramline spacing");
            }
        }

        [Test]
        public void Swaths_LightenAndThinStripesAcrossTheRows_InBothCopies()
        {
            var climate = Climate(Track(ScriptableObject.CreateInstance<RowCrop>()));
            var layout = MapFixture.Layout(MapFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Sown", 7, 0f));
            land.ClaimWhere(land.Bounds, field, p => InField(p, 0f) || InField(-p, 0f));
            var edges = new MapEdgeField(layout, land, climate);
            var heights = new MapHeights(129, layout.TerrainBounds);
            var shapes = MapFixture.Shapes(layout, climate);
            climate.groundTint = new MapClimateGroundTint { gradientLength = 260f, soilSize = 140f };
            var plain = new MapGroundTint(layout, land, edges, shapes, heights, climate.groundTint);
            climate.Surface("Sown").swaths = new MapSwaths { spacing = 9f, width = 3f, light = 0.05f, plants = 0.4f };
            var swathed = new MapGroundTint(layout, land, edges, shapes, heights, climate.groundTint);
            var lines = new MapRowLines(layout.Symmetry, edges, 0.25f);

            var (lighter, samples) = (0, 0);
            for (var x = 24f; x < 56f; x += 0.25f)
            {
                var p = new Vector2(x, 40f);
                var lift = swathed.At(p).g - plain.At(p).g;
                Assert.AreEqual(lift, swathed.At(-p).g - plain.At(-p).g, 1e-4f, $"swath at {p} and its copy");
                Assert.AreEqual(lift, swathed.At(p + Vector2.up * 7f).g - plain.At(p + Vector2.up * 7f).g, 1e-4f,
                    $"a swath runs along the rows at {p}");
                if (lift > 0.049f || lift < 0.001f)
                {
                    var kept = (lift > 0.025f ? 0.4f : 1f) * (1f - lines.Tramline(field, p));
                    Assert.AreEqual(kept, lines.Plants(field, p), 0.02f, $"plants standing at {p}");
                }

                lighter += lift > 0.025f ? 1 : 0;
                samples++;
            }

            Assert.That(lighter / (float)samples, Is.InRange(0.25f, 0.42f), "a 3 m swath every 9 m");
        }

        private static MapClimate Climate(RowCrop crop)
        {
            var tramlines = new MapTramlines { spacing = Spacing, width = Width, layer = "Wheels" };
            return new MapClimate
            {
                name = "Test",
                groundSurface = "Ground",
                layers = new List<MapClimateLayer> { new() { key = "Soil" }, new() { key = "Wheels" } },
                surfaces = new List<MapClimateSurface>
                {
                    new() { key = "Ground", layer = "Soil" },
                    new() { key = "Sown", layer = "Soil", rowCrop = "Crop", tramlines = tramlines },
                },
                rowCrops = new List<MapClimateRowCrop> { new() { key = "Crop", crop = crop } },
            };
        }

        /// <summary>Whether the plant's cell is sown by <paramref name="field"/>.</summary>
        private static bool Sown(MapCropBake bake, MapLand land, Vector2 position, int field) =>
            land.TryCell(position, out var x, out var z) && bake.Id(x, z) == RowCropFields.FieldId(field);

        /// <summary>Inside the authored 40 m field by <paramref name="inset"/>.</summary>
        private static bool InField(Vector2 p, float inset) =>
            p.x > 20f + inset && p.x < 60f - inset && p.y > 20f + inset && p.y < 60f - inset;
    }
}
