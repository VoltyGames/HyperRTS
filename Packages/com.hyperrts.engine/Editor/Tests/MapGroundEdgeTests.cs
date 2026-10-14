using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests
{
    /// <summary>How surfaces meet: edge bands and the macro tint following the splat.</summary>
    public class MapGroundEdgeTests
    {
        private const int Resolution = 512;

        [Test]
        public void EdgeWidth_SetsTheWidthOfTheBlendBand()
        {
            var narrow = BandWidth(1f);
            var wide = BandWidth(6f);

            Assert.LessOrEqual(narrow, 1.8f, "a 1 m edge blends over about a metre");
            Assert.That(wide, Is.InRange(4f, 6.5f), "a 6 m edge blends over most of 6 m");
            Assert.Greater(wide, narrow * 3f);
        }

        [Test]
        public void Macro_IsEqualAtSymmetryCopies()
        {
            var climate = Climate(2f, 1.5f);
            var layout = MapRoadFixture.Layout(MapRoadFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "A", 5));
            var wood = land.Add(new MapLandRegion(MapLandKind.Wood, "B", 9));
            var plot = new Rect(-50f, -80f, 60f, 60f);
            land.ClaimEach(land.Bounds, p => Pick(p, plot, field, wood));
            var heights = Heights(layout, p => 2f * Mathf.Cos(p.x / 15f) * Mathf.Cos(p.y / 20f));
            var edges = new MapEdgeField(layout, land, climate);
            var macro = new MapMacro(layout, land, edges, MapRoadFixture.Shapes(layout, climate), heights,
                climate.macro);

            for (var x = -110f; x < 110f; x += 7.3f)
            {
                for (var z = -110f; z < 110f; z += 6.1f)
                {
                    var p = new Vector2(x, z);
                    var (here, there) = (macro.At(p), macro.At(-p));
                    var gap = Gap(here, there);
                    Assert.Less(gap, 2e-3f, $"macro at {p} and its copy");
                }
            }
        }

        [Test]
        public void Macro_TintEdgeFollowsTheSplatEdge()
        {
            var climate = Climate(0.5f, 2.5f);
            var (layout, land) = Halves(2);
            var (_, recoloured) = Halves(3);
            var heights = Flat(layout);
            var shapes = MapRoadFixture.Shapes(layout, climate);
            var edges = new MapEdgeField(layout, land, climate);
            var splat = MapSplatPainter.Paint(layout, edges, heights, climate, Resolution);
            var macro = new MapMacro(layout, land, edges, shapes, heights, climate.macro);
            var recolouredEdges = new MapEdgeField(layout, recoloured, climate);
            var other = new MapMacro(layout, recoloured, recolouredEdges, shapes, heights, climate.macro);
            var b = climate.LayerIndex("B");
            var area = layout.TerrainBounds;
            var texel = area.size / Resolution;
            var wandered = 0;
            for (var z = 0; z < Resolution; z += 3)
            {
                for (var x = Resolution / 2 - 12; x < Resolution / 2 + 12; x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), texel);
                    var gap = Gap(macro.At(p), other.At(p));
                    var splatB = splat[z, x, b];
                    Assert.IsFalse(splatB <= 0f && gap > 1e-6f, $"B's tint reaches {p}, where its texture doesn't");
                    Assert.IsFalse(splatB > 0.999f && gap < 1e-4f, $"B's texture covers {p} without its tint");
                    wandered += splatB > 0.5f && p.x < -0.6f ? 1 : 0;
                }
            }

            Assert.Greater(wandered, 0, "the jitter moves the edge off the straight line");
        }

        [Test]
        public void SurfaceEdge_IsRequired()
        {
            var climate = MapRoadFixture.Climate();
            climate.Surface("Meadow").edgeWidth = 0f;
            climate.Surface("Dirt").edgeJitter = 0f;
            var issues = new MapIssues();

            MapAssetCheck.Check(climate, new MapPalette(), issues);

            StringAssert.Contains("surface 'Meadow': edgeWidth 0", issues.ToString());
            StringAssert.Contains("surface 'Dirt': edgeJitter 0", issues.ToString());
        }

        private static float BandWidth(float width)
        {
            var climate = Climate(width, 0.05f);
            var (layout, land) = Halves(2);
            var edges = new MapEdgeField(layout, land, climate);
            var splat = MapSplatPainter.Paint(layout, edges, Flat(layout), climate, Resolution);
            var a = climate.LayerIndex("A");
            var texel = layout.TerrainBounds.width / Resolution;
            var blended = 0;
            var rows = 0;
            for (var z = 40; z < Resolution - 40; z += 37, rows++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    var weight = splat[z, x, a];
                    blended += weight > 0.02f && weight < 0.98f ? 1 : 0;
                }
            }

            return blended * texel / rows;
        }

        /// <summary>A field west of x = 0 and another east of it; <paramref name="eastSeed"/> recolours the east one.</summary>
        private static (MapLayout Layout, MapLand Land) Halves(int eastSeed)
        {
            var layout = MapRoadFixture.Layout(MapRoadFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var west = land.Add(new MapLandRegion(MapLandKind.Field, "A", 1));
            var east = land.Add(new MapLandRegion(MapLandKind.Field, "B", eastSeed));
            land.ClaimEach(land.Bounds, p => p.x < 0f ? west : east);
            return (layout, land);
        }

        /// <summary>The plot and its turned copy are the field; a disc and its copy the wood; the rest unclaimed.</summary>
        private static int Pick(Vector2 p, Rect plot, int field, int wood)
        {
            if (plot.Contains(p) || plot.Contains(-p))
            {
                return field;
            }

            var centre = new Vector2(75f, -60f);
            var inWood = Vector2.Distance(p, centre) < 25f || Vector2.Distance(-p, centre) < 25f;
            return inWood ? wood : MapLand.Unclaimed;
        }

        private static MapClimate Climate(float width, float jitter)
        {
            var climate = new MapClimate { name = "Test", slopeStart = 30f, slopeFull = 40f };
            MapClimateFixture.AddSurfaces(climate, width, jitter, "Meadow", "A", "B");

            climate.groundSurface = climate.groundVariation = climate.slopeSurface = "Meadow";
            climate.macro = new MapClimateMacro
            {
                regionLight = 0.09f, regionWarmth = 0.06f, gradientLight = 0.025f, gradientWarmth = 0.012f,
                gradientWavelength = 260f, hollowDarkening = 0.045f, hollowWetness = 0.3f,
            };
            return climate;
        }

        private static MapHeights Flat(MapLayout layout) => Heights(layout, _ => 0f);

        private static MapHeights Heights(MapLayout layout, System.Func<Vector2, float> height)
        {
            var heights = new MapHeights(257, layout.TerrainBounds);
            for (var z = 0; z < heights.Resolution; z++)
            {
                for (var x = 0; x < heights.Resolution; x++)
                {
                    heights[x, z] = height(heights.Position(x, z));
                }
            }

            return heights;
        }

        private static float Weight(float[,,] splat, MapLayout layout, Vector2 p, int layer)
        {
            var area = layout.TerrainBounds;
            var x = Mathf.FloorToInt((p.x - area.xMin) / area.width * Resolution);
            var z = Mathf.FloorToInt((p.y - area.yMin) / area.height * Resolution);
            return splat[z, x, layer];
        }

        private static float Gap(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));
    }
}
