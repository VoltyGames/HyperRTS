using System.Collections.Generic;
using System.Linq;
using HyperRTS.Core;
using HyperRTS.Presentation.Environment.Vegetation.Grass;
using HyperRTS.Presentation.Environment.Vegetation.Scatter;
using HyperRTS.Presentation.Quality;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace HyperRTS.Presentation.Tests.Environment.Vegetation
{
    /// <summary>Ground cover: grass fields and their GPU kernel rules, scatter tile culling, the building footprint mask and terrain detail.</summary>
    public class GroundCoverTests
    {
        private GrassField _field;
        private Mesh _mesh;
        private Material _material;
        private Texture2DArray _coverage;

        [SetUp]
        public void SetUp()
        {
            _mesh = new Mesh();
            _mesh.SetVertices(new[] { Vector3.zero, Vector3.up * 0.8f, Vector3.right });
            _mesh.SetTriangles(new[] { 0, 1, 2 }, 0);
            _material = new Material(Shader.Find("Hidden/HyperRTS/FoliageGrass"));
            _coverage = new Texture2DArray(4, 4, 2, TextureFormat.R8, false, true);
            _field = ScriptableObject.CreateInstance<GrassField>();
            _field.coverage = _coverage;
            _field.area = new Rect(-32f, -32f, 64f, 64f);
            _field.plants = new List<GrassPlant> { Plant("Grass", 2, 9f), Plant("Thistle", 3, 0.25f) };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_field);
            Object.DestroyImmediate(_coverage);
            Object.DestroyImmediate(_material);
            Object.DestroyImmediate(_mesh);
        }

        [Test]
        public void Slots_RunPlantByPlant_AModelPerBandEach_SpacedForTheDensity()
        {
            var gpuPlants = _field.GpuPlants();

            Assert.AreEqual((2 + 3) * GrassBands.Bands, _field.SlotCount);
            Assert.AreEqual(0, gpuPlants[0].firstSlot);
            Assert.AreEqual(2 * GrassBands.Bands, gpuPlants[1].firstSlot);
            Assert.AreEqual(3, gpuPlants[1].variants);
            CollectionAssert.AreEqual(new[] { 0, 2 * GrassBands.Bands, 5 * GrassBands.Bands }, _field.FirstSlots());
            Assert.AreNotEqual(gpuPlants[0].seed, gpuPlants[1].seed);
            Assert.AreEqual(1f / 3f, gpuPlants[0].spacing, 1e-5f);
            Assert.AreEqual(2f, _field.plants[1].Spacing, 1e-5f);
            Assert.AreEqual(0.8f * 1.2f, _field.plants[0].Tallest, 1e-5f);
        }

        [Test]
        public void TileRects_StepFromTheAreasCorner()
        {
            var rect = _field.TileRect(new ScatterTile { x = 2, z = 1 });

            Assert.AreEqual(new Rect(-32f + 2f * ScatterTiles.Size, -32f + ScatterTiles.Size, 16f, 16f), rect);
        }

        [Test]
        public void Problems_NameMissingModelsAndSlicesThatDontMatchThePlants()
        {
            CollectionAssert.IsEmpty(_field.Problems());

            _field.plants[1].meshes.Clear();
            _field.plants.Add(Plant("Yarrow", 1, 0f));

            var problems = string.Join("\n", _field.Problems());
            StringAssert.Contains("coverage has 2 slices for 3 plants", problems);
            StringAssert.Contains("plant 'Thistle': meshes is empty", problems);
            StringAssert.Contains("plant 'Yarrow': density 0 must be above 0", problems);
        }

        [Test]
        public void Keep_IsFullNear_ThinsSteadily_AndGrowCoversTheThinnedPlants()
        {
            Assert.AreEqual(1f, GrassBands.Keep(0f, 200f));
            Assert.AreEqual(1f, GrassBands.Keep(GrassBands.ThinStart * 200f, 200f), 1e-5f);
            Assert.AreEqual(GrassBands.FarKeep, GrassBands.Keep(200f, 200f), 1e-5f);
            var last = 1f;
            for (var range = 0f; range <= 200f; range += 10f)
            {
                var keep = GrassBands.Keep(range, 200f);
                Assert.That(keep, Is.LessThanOrEqualTo(last), $"keep at {range} m");
                last = keep;
            }

            Assert.AreEqual(Mathf.Sqrt(2f), GrassBands.Grow(0.5f), 1e-5f);
            Assert.AreEqual(GrassBands.MaxGrow, GrassBands.Grow(GrassBands.FarKeep));
            Assert.AreEqual(1f, GrassBands.Drawn(0f, 200f));
            Assert.AreEqual(GrassBands.FarKeep * (1f + GrassBands.KeepEdge), GrassBands.Drawn(200f, 200f), 1e-5f);
        }

        [TestCase(0f, 40f, 30f, true, true)]
        [TestCase(40f, 60f, 30f, false, true)]
        [TestCase(0f, 20f, 30f, true, false)]
        [TestCase(0f, 20f, 0f, false, true)]
        [TestCase(32f, 36f, 30f, true, true)]
        public void Bands_HoldTheTilesThatReachThem(float near, float far, float shadows, bool inNear, bool inFar)
        {
            Assert.AreEqual(inNear, GrassBands.InBand(GrassBands.Near, shadows, near, far));
            Assert.AreEqual(inFar, GrassBands.InBand(GrassBands.Far, shadows, near, far));
        }

        [TestCase(0f)]
        [TestCase(5000f)]
        public void Capacity_LeavesRoomOverTheExpectedCount(float expected) =>
            Assert.That(GrassBands.Capacity(expected), Is.GreaterThan(expected * 1.05f + 3f));

        [Test]
        public void GpuStructs_MatchTheKernelsLayouts()
        {
            Assert.AreEqual(40, GrassPlantGpu.Stride);
            Assert.AreEqual(28, ScatterJob.Stride);
        }

        [TestCase(0f, 160f, 160f)]
        [TestCase(40f, 160f, 40f)]
        [TestCase(240f, 160f, 160f)]
        public void DistanceAt_TakesTheShorterOfReachAndTheQualityDistance(float reach, float distance, float drawn)
        {
            var plant = new GrassPlant { reach = reach };
            Assert.AreEqual(drawn, plant.DistanceAt(distance));
            Assert.AreEqual(reach, plant.ToGpu(0, 0).reach);
        }

        [TestCase(0f, 0f, 0.29f)]
        [TestCase(-48f, 160f, 0.5f)]
        [TestCase(32f, -16f, 1.3f)]
        public void Jobs_CoverEveryCellWhosePlantCanFallInTheTile(float x, float z, float spacing)
        {
            var tile = new Rect(x, z, ScatterTiles.Size, ScatterTiles.Size);
            var job = ScatterJob.Square(tile, 3, spacing);

            Assert.AreEqual(3, job.id);
            Assert.AreEqual(tile.min, job.tileMin);
            for (var probe = 0f; probe < ScatterTiles.Size; probe += spacing * 0.37f)
            {
                var cell = new Vector2Int(Mathf.FloorToInt((x + probe) / spacing), Mathf.FloorToInt((z + probe) / spacing));
                var offset = cell - job.first;
                Assert.That(offset.x, Is.InRange(0, job.count.x - 1), $"cell {cell}");
                Assert.That(offset.y, Is.InRange(0, job.count.y - 1), $"cell {cell}");
            }

            Assert.That(job.Threads, Is.LessThanOrEqualTo(Mathf.Pow(ScatterTiles.Size / spacing + 2f, 2f)));
        }

        [Test]
        public void ScatterTile_BoundsWidenByTheOverhang_AndDistancesReachTheBox()
        {
            var bounds = ScatterTiles.TileBounds(new Rect(0f, 0f, 16f, 16f), 1f, 3f, 2f);
            Assert.AreEqual(new Vector3(-ScatterTiles.Overhang, 1f, -ScatterTiles.Overhang), bounds.min);
            Assert.AreEqual(new Vector3(16f + ScatterTiles.Overhang, 5f, 16f + ScatterTiles.Overhang), bounds.max);

            var far = ScatterTiles.TileBounds(new Rect(100f, 0f, 16f, 16f), 0f, 0f, 2f);
            var (nearest, farthest) = ScatterTiles.Distances(far, new Vector3(0f, 1f, 8f));
            Assert.AreEqual(100f - ScatterTiles.Overhang, nearest, 1e-4f);
            Assert.Greater(farthest, 116f);
        }

        [Test]
        public void TerrainDetail_GivesEachLevelBelowUltraItsOwnKeyword_AndUltraNone()
        {
            var keywords = new[] { TerrainQuality.Low, TerrainQuality.Medium, TerrainQuality.High }
                .Select(TerrainDetail.KeywordFor).ToArray();

            Assert.That(keywords, Is.All.Not.Null);
            Assert.That(keywords, Is.Unique);
            Assert.AreEqual(TerrainDetail.NoParallaxKeyword, TerrainDetail.KeywordFor(TerrainQuality.High));
            Assert.IsNull(TerrainDetail.KeywordFor(TerrainQuality.Ultra), "the shader's full path");
        }

        [Test]
        public void FootprintMask_MarksTheCellsUnderATurnedBuilding()
        {
            using var kit = FootprintWorld(out var system);
            Building(kit.EntityManager, float3.zero, math.PI / 4f, new float2(10f, 2f));

            system.Update();

            var mask = system.Mask;
            Assert.IsTrue(mask.IsMarked(float2.zero), "under its middle");
            Assert.IsTrue(mask.IsMarked(new float2(3f, -3f)), "along its turned length");
            Assert.IsFalse(mask.IsMarked(new float2(3f, 3f)), "off its side, inside its world box");
            Assert.IsFalse(mask.IsMarked(new float2(20f, 20f)), "open ground");
        }

        [Test]
        public void FootprintMask_RedrawsOnlyWhenABuildingGoes()
        {
            using var kit = FootprintWorld(out var system);
            var building = Building(kit.EntityManager, new float3(10f, 0f, 10f), 0f, new float2(6f, 6f));
            system.Update();
            var drawn = BuildingFootprintMask.Version;
            system.Update();
            Assert.AreEqual(drawn, BuildingFootprintMask.Version, "unchanged buildings");

            kit.EntityManager.DestroyEntity(building);
            system.Update();

            Assert.IsFalse(system.Mask.IsMarked(new float2(10f, 10f)));
            Assert.AreNotEqual(drawn, BuildingFootprintMask.Version, "scattered plants regrow");
        }

        private static PresentationWorldKit FootprintWorld(out BuildingFootprintSystem system)
        {
            var kit = new PresentationWorldKit("Building Footprint Tests");
            system = kit.World.CreateSystemManaged<BuildingFootprintSystem>();
            kit.EntityManager.CreateSingleton(new MapSettings
            {
                Min = new float2(-50f), Size = new float2(100f), NavCellSize = 1f,
            });
            return kit;
        }

        private static Entity Building(EntityManager em, float3 position, float yaw, float2 size)
        {
            var building = em.CreateEntity();
            em.AddComponent<BuildingTag>(building);
            em.AddComponentData(building, new NavObstacle { Size = size });
            em.AddComponentData(building, LocalTransform.FromPositionRotation(position, quaternion.RotateY(yaw)));
            return building;
        }

        private GrassPlant Plant(string name, int models, float density)
        {
            var plant = new GrassPlant
            {
                name = name,
                material = _material,
                width = new Vector2(0.8f, 1.2f),
                height = new Vector2(0.8f, 1.2f),
                density = density,
            };
            for (var i = 0; i < models; i++)
            {
                plant.meshes.Add(_mesh);
            }

            return plant;
        }
    }
}
