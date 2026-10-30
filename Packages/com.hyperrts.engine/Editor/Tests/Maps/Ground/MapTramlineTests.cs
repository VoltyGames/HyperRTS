using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Presentation.Environment.RowCrops;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>Row crops leave bare the tramlines the ground paints, in every copy.</summary>
    public class MapTramlineTests : TemplateFixture
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
            var cover = new MapTramlineCover(layout.Symmetry, new MapEdgeField(layout, land, climate), climate, 0.02f);

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
                        Assert.AreEqual(gap, cover.Cover(field, position) > 0.5f, $"plant at {position}");
                        gaps += gap ? 1 : 0;
                        plants++;
                    }
                }

                Assert.That(gaps / (float)plants, Is.InRange(0.5f * Width / Spacing, 4f * Width / Spacing),
                    "two wheel lines in every tramline spacing");
            }
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
