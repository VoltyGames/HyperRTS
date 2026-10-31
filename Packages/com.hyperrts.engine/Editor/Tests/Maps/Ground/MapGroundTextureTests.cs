using HyperRTS.Editor.Maps;
using HyperRTS.Editor.Maps.Checks;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Ground;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Layout;
using HyperRTS.Editor.Maps.Texturing;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Maps.Ground
{
    /// <summary>Ground textures: edge bands, surface mixes, the ground tint and each layer's terrain parameters.</summary>
    public class MapGroundTextureTests
    {
        private const int Resolution = 512;
        private TerrainLayer _terrainLayer;

        [SetUp]
        public void SetUp() => _terrainLayer = new TerrainLayer { normalScale = 0.8f, tileSize = new Vector2(4f, 2f) };

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_terrainLayer);

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
        public void GroundTint_IsEqualAtSymmetryCopies()
        {
            var climate = Climate(2f, 1.5f);
            var layout = MapFixture.Layout(MapFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "A", 5));
            var wood = land.Add(new MapLandRegion(MapLandKind.Wood, "B", 9));
            var plot = new Rect(-50f, -80f, 60f, 60f);
            land.ClaimEach(land.Bounds, p => Pick(p, plot, field, wood));
            var heights = Heights(layout, p => 2f * Mathf.Cos(p.x / 15f) * Mathf.Cos(p.y / 20f));
            var edges = new MapEdgeField(layout, land, climate);
            var tint = new MapGroundTint(layout, land, edges, MapFixture.Shapes(layout, climate), heights,
                climate.groundTint);

            for (var x = -110f; x < 110f; x += 7.3f)
            {
                for (var z = -110f; z < 110f; z += 6.1f)
                {
                    var p = new Vector2(x, z);
                    Assert.Less(Gap(tint.At(p), tint.At(-p)), 2e-3f, $"tint at {p} and its copy");
                }
            }
        }

        [Test]
        public void GroundTintMap_HasTwoTexelsPerMetre()
        {
            var area = new Rect(-375f, -375f, 750f, 750f);

            Assert.AreEqual(1500, MapGroundTintPainter.Size(area));
            Assert.AreEqual(0, MapGroundTintPainter.Size(new Rect(0f, 0f, 101f, 101f)) % 4, "block aligned");
        }

        [Test]
        public void GroundTint_SoilColourVariesAField()
        {
            var climate = Climate(2f, 1.5f);
            var layout = MapFixture.Layout(MapFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "A", 5));
            land.ClaimEach(land.Bounds, _ => field);
            var heights = Flat(layout);
            var edges = new MapEdgeField(layout, land, climate);
            var shapes = MapFixture.Shapes(layout, climate);
            var withSoil = new MapGroundTint(layout, land, edges, shapes, heights, climate.groundTint);
            var untinted = Climate(2f, 1.5f).groundTint;
            untinted.soilLight = untinted.soilWarmth = untinted.soilWetness = 1e-6f;
            var plain = new MapGroundTint(layout, land, edges, shapes, heights, untinted);
            var largest = 0f;
            for (var x = -100f; x < 100f; x += 9.7f)
            {
                for (var z = -100f; z < 100f; z += 8.3f)
                {
                    var p = new Vector2(x, z);
                    largest = Mathf.Max(largest, Gap(withSoil.At(p), plain.At(p)));
                }
            }

            Assert.Greater(largest, 0.03f, "soil colour shifts light, warmth or moisture across a field");
        }

        [Test]
        public void SurfaceMixes_PaintTheirLayerSymmetrically()
        {
            var climate = Climate(2f, 1.5f);
            climate.Surface("A").mixes.Add(new MapSurfaceMix { layer = "B", coverage = 0.3f, size = 30f });
            var layout = MapFixture.Layout(MapFixture.Spec());
            var land = new MapLand(layout.TerrainBounds);
            var field = land.Add(new MapLandRegion(MapLandKind.Field, "A", 5));
            land.ClaimEach(land.Bounds, _ => field);
            var textureWeights = MapGroundTexturePainter.Paint(layout, new MapEdgeField(layout, land, climate), Flat(layout), climate,
                Resolution);
            var b = climate.LayerIndex("B");
            var covered = 0;
            for (var x = -100f; x < 100f; x += 5.3f)
            {
                for (var z = -100f; z < 100f; z += 4.7f)
                {
                    var p = new Vector2(x, z);
                    var here = Weight(textureWeights, layout, p, b);
                    Assert.AreEqual(here, Weight(textureWeights, layout, -p, b), 0.05f, $"mix at {p} and its copy");
                    covered += here > 0.5f ? 1 : 0;
                }
            }

            Assert.That(covered / (38f * 43f), Is.InRange(0.1f, 0.5f), "mixes cover about their share");
        }

        [Test]
        public void GroundTint_EdgeFollowsTheGroundTextureEdge()
        {
            var climate = Climate(0.5f, 2.5f);
            var (layout, land) = Halves(2);
            var (_, recoloured) = Halves(3);
            var heights = Flat(layout);
            var shapes = MapFixture.Shapes(layout, climate);
            var edges = new MapEdgeField(layout, land, climate);
            var textureWeights = MapGroundTexturePainter.Paint(layout, edges, heights, climate, Resolution);
            var tint = new MapGroundTint(layout, land, edges, shapes, heights, climate.groundTint);
            var recolouredEdges = new MapEdgeField(layout, recoloured, climate);
            var other = new MapGroundTint(layout, recoloured, recolouredEdges, shapes, heights, climate.groundTint);
            var b = climate.LayerIndex("B");
            var area = layout.TerrainBounds;
            var texel = area.size / Resolution;
            var wandered = 0;
            for (var z = 0; z < Resolution; z += 3)
            {
                for (var x = Resolution / 2 - 12; x < Resolution / 2 + 12; x++)
                {
                    var p = area.min + Vector2.Scale(new Vector2(x + 0.5f, z + 0.5f), texel);
                    var gap = Gap(tint.At(p), other.At(p));
                    var weightB = textureWeights[z, x, b];
                    Assert.IsFalse(weightB <= 0f && gap > 1e-6f, $"B's tint reaches {p}, where its texture doesn't");
                    Assert.IsFalse(weightB > 0.999f && gap < 1e-4f, $"B's texture covers {p} without its tint");
                    wandered += weightB > 0.5f && p.x < -0.6f ? 1 : 0;
                }
            }

            Assert.Greater(wandered, 0, "the wander moves the edge off the straight line");
        }

        [Test]
        public void SurfaceEdge_IsRequired()
        {
            var climate = MapFixture.Climate();
            climate.Surface("Meadow").edgeWidth = 0f;
            climate.Surface("Dirt").edgeWander = 0f;
            var issues = new MapIssues();

            MapAssetCheck.Check(climate, MapFixture.Item<MapPalette>("Palette"), issues);

            StringAssert.Contains("surface 'Meadow': edgeWidth 0", issues.ToString());
            StringAssert.Contains("surface 'Dirt': edgeWander 0", issues.ToString());
        }

        [Test]
        public void TerrainParams_AFreeLayerTurnsAndShiftsAcrossRows_ARowLayerMapsByItsOwnTurnOnly()
        {
            var free = MapTerrainParams.Surface(Layer(false));
            var rows = MapTerrainParams.Surface(Layer(true));
            var mapping = MapTerrainParams.Mapping(Layer(true));

            Assert.AreEqual(1.5f, free.r, 1e-5f);
            Assert.AreEqual(0.8f, free.g, 1e-5f);
            Assert.AreEqual(0.6f, free.b, 1e-5f, "rotation strength");
            Assert.AreEqual(1f, free.a, "offsets may cross rows");
            Assert.AreEqual(0f, rows.b, "a row layer never turns");
            Assert.AreEqual(0f, rows.a, "flagged as following rows");
            Assert.AreEqual(0f, mapping.r, 1e-5f, "cos 90");
            Assert.AreEqual(1f, mapping.g, 1e-5f, "sin 90");
            Assert.AreEqual(0.25f, mapping.b, 1e-5f);
            Assert.AreEqual(0.5f, mapping.a, 1e-5f);
        }

        [Test]
        public void TerrainParams_Cover_IsTheAuthoredGrass_AndMarksOnlyTheSlopeLayerTriplanar()
        {
            Assert.AreEqual(0.4f, MapTerrainParams.Cover(Layer(false), false).r, 1e-5f);
            Assert.AreEqual(1f, MapTerrainParams.Cover(Layer(false), true).g, "the slope layer");
            Assert.AreEqual(0f, MapTerrainParams.Cover(Layer(false), false).g, "any other layer");
        }

        private static float BandWidth(float width)
        {
            var climate = Climate(width, 0.05f);
            var (layout, land) = Halves(2);
            var edges = new MapEdgeField(layout, land, climate);
            var textureWeights = MapGroundTexturePainter.Paint(layout, edges, Flat(layout), climate, Resolution);
            var a = climate.LayerIndex("A");
            var texel = layout.TerrainBounds.width / Resolution;
            var blended = 0;
            var rows = 0;
            for (var z = 40; z < Resolution - 40; z += 37, rows++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    var weight = textureWeights[z, x, a];
                    blended += weight > 0.02f && weight < 0.98f ? 1 : 0;
                }
            }

            return blended * texel / rows;
        }

        /// <summary>A field west of x = 0 and another east of it; <paramref name="eastSeed"/> recolours the east one.</summary>
        private static (MapLayout Layout, MapLand Land) Halves(int eastSeed)
        {
            var layout = MapFixture.Layout(MapFixture.Spec());
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

        private MapClimateLayer Layer(bool followsRows) => MapFixture.Item<MapClimateLayer>("Layer", i =>
        {
            i.terrainLayer = _terrainLayer;
            i.followsRows = followsRows;
            i.heightBlend = 1.5f;
            i.antiTiling = 0.6f;
            i.rotation = 90f;
            i.grass = 0.4f;
        });

        private static MapClimate Climate(float width, float wander)
        {
            var climate = MapFixture.Item<MapClimate>("Test", i => { i.slopeStart = 30f; i.slopeFull = 40f; });
            MapFixture.AddSurfaces(climate, width, wander, "Meadow", "A", "B");
            climate.groundSurface = climate.slopeSurface = "Meadow";
            climate.groundTint = new MapClimateGroundTint
            {
                regionLight = 0.09f, regionWarmth = 0.06f, gradientLight = 0.025f, gradientWarmth = 0.012f,
                gradientLength = 260f, hollowDarkening = 0.045f, hollowWetness = 0.3f, soilSize = 140f,
                soilLight = 0.05f, soilWarmth = 0.03f, soilWetness = 0.25f,
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

        private static float Weight(float[,,] textureWeights, MapLayout layout, Vector2 p, int layer)
        {
            var area = layout.TerrainBounds;
            var x = Mathf.FloorToInt((p.x - area.xMin) / area.width * Resolution);
            var z = Mathf.FloorToInt((p.y - area.yMin) / area.height * Resolution);
            return textureWeights[z, x, layer];
        }

        private static float Gap(Color a, Color b) =>
            Mathf.Max(Mathf.Abs(a.r - b.r), Mathf.Abs(a.g - b.g), Mathf.Abs(a.b - b.b), Mathf.Abs(a.a - b.a));
    }
}
