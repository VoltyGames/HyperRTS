using HyperRTS.Editor.Maps.Geometry;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Symmetry;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    public class MapRowPainterTests
    {
        private const int Half = 16;
        private const float Angle = 30f;

        [Test]
        public void SectorField_HoldsItsRowAngleAsAUnitVector()
        {
            var land = Land();
            var symmetry = MapSymmetry.Parse(MapSymmetry.MirrorXName);
            var pixels = MapRowPainter.Paint(land, symmetry);

            var along = MapGeometry.Forward(Angle);
            var checkedCells = 0;
            land.ForEach((x, z, center) =>
            {
                if (!IsField(land, x, z) || !symmetry.InSector(center))
                {
                    return;
                }

                AssertDirection(along, pixels[z * land.Width + x], $"cell ({x}, {z})");
                checkedCells++;
            });
            Assert.Greater(checkedCells, 0);
        }

        [TestCase(MapSymmetry.MirrorXName)]
        [TestCase(MapSymmetry.MirrorZName)]
        [TestCase("rotate-180")]
        public void CopiedField_TurnsThroughItsCopy(string symmetryName)
        {
            var land = Land();
            var symmetry = MapSymmetry.Parse(symmetryName);
            var pixels = MapRowPainter.Paint(land, symmetry);

            land.ForEach((x, z, center) =>
            {
                if (!IsField(land, x, z))
                {
                    return;
                }

                symmetry.ToSector(center, out var copy);
                var expected = symmetry.Copies[copy].Apply(MapGeometry.Forward(Angle));
                AssertDirection(expected, pixels[z * land.Width + x], $"cell ({x}, {z})");
            });
        }

        [Test]
        public void MirroredCopy_MirrorsTheDirection()
        {
            var land = Land();
            var pixels = MapRowPainter.Paint(land, MapSymmetry.Parse(MapSymmetry.MirrorXName));

            var (west, east) = (pixels[Half * land.Width + 2], pixels[Half * land.Width + land.Width - 3]);
            Assert.AreEqual(1f, west.r + east.r, 1e-5f, "x flips");
            Assert.AreEqual(west.g, east.g, 1e-5f, "z stays");
        }

        [Test]
        public void LandWithoutRows_IsNeutral()
        {
            var land = Land();
            var pixels = MapRowPainter.Paint(land, MapSymmetry.Parse(MapSymmetry.MirrorXName));

            land.ForEach((x, z, _) =>
            {
                if (IsField(land, x, z))
                {
                    return;
                }

                Assert.AreEqual(MapRowPainter.NoRows, pixels[z * land.Width + x], $"cell ({x}, {z})");
            });
        }

        private static void AssertDirection(Vector2 expected, Color pixel, string cell)
        {
            Assert.AreEqual(expected.x * 0.5f + 0.5f, pixel.r, 1e-4f, cell);
            Assert.AreEqual(expected.y * 0.5f + 0.5f, pixel.g, 1e-4f, cell);
        }

        private static bool IsField(MapLand land, int x, int z) => land.Region(x, z)?.Kind == MapLandKind.Field;

        /// <summary>A 32 m map: a field with rows at 30° on both halves and a meadow strip down the middle.</summary>
        private static MapLand Land()
        {
            var land = new MapLand(new Rect(-Half, -Half, Half * 2, Half * 2));
            var meadow = land.Add(new MapLandRegion(MapLandKind.Meadow, "Grass"));
            land.ClaimWhere(land.Bounds, meadow, p => Mathf.Abs(p.x) < 3f);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "Sown", 7, Angle));
            land.ClaimWhere(land.Bounds, field, p => Mathf.Abs(p.x) > 4f);
            return land;
        }
    }
}
