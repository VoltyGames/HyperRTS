using System.Collections.Generic;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Players;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Buildings.Placement
{
    /// <summary>
    /// Dragged lines of wall segments: axes, diagonals, skipped cells, the building gap, running out mid-line and the
    /// builders' queue.
    /// </summary>
    public class LinePlacementTests : SimulationFixture
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        private ResourceType _supplies;

        [SetUp]
        public void Prepare()
        {
            _supplies = World.CreateSupplies();
        }

        private Entity WallPrefab(int funds = 1000)
        {
            World.AddResources(1, _supplies, funds);
            var prefab = World.MakePrefab(World.SpawnBuilding(0, Far, new float2(2f, 1.2f), name: "Wall"));
            World.EntityManager.AddComponent<LinePlacement>(prefab);
            World.SetCost(prefab, _supplies, 100);
            return prefab;
        }

        private Entity Builder(Entity prefab, float3 position) =>
            World.MakeBuilder(World.SpawnUnit(1, position), prefab);

        /// <summary>The player's sites, ordered along X then Z.</summary>
        private NativeList<float3> SitePositions()
        {
            var result = new NativeList<float3>(Allocator.Temp);
            foreach (var site in World.All<ConstructionProgress>())
            {
                if (World.Get<Faction>(site).Value == 1)
                {
                    result.Add(World.Get<LocalTransform>(site).Position);
                }
            }

            result.Sort(new AlongLine());
            return result;
        }

        [Test]
        public void PlaceLine_AlongX_PlacesOneSegmentPerFootprintLength_FacingTheCamera()
        {
            var prefab = WallPrefab();
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 0.3f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(4, sites.Length);
            for (var i = 0; i < sites.Length; i++)
            {
                MathAssert.Near(new float3(4f + 2f * i, 0f, 0.5f), sites[i]);
            }

            Assert.AreEqual(600, World.GetResources(1, _supplies));
            var rotation = World.Get<LocalTransform>(World.All<ConstructionProgress>()[0]).Rotation;
            var front = math.rotate(rotation, new float3(0f, 0f, 1f));
            var expected = math.rotate(quaternion.RotateY(PlacementMath.CameraYaw), new float3(0f, 0f, 1f));
            MathAssert.Near(expected.xz, front.xz, 0.001f);
        }

        [Test]
        public void PlaceLine_AlongZ_TurnsEachSegmentAQuarter()
        {
            var prefab = WallPrefab();
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4.3f, 0f, 4f), new float3(4.32f, 0f, 10.2f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(4, sites.Length);
            for (var i = 0; i < sites.Length; i++)
            {
                MathAssert.Near(new float3(4.5f, 0f, 4f + 2f * i), sites[i]);
            }

            var site = World.All<ConstructionProgress>()[0];
            var transform = World.Get<LocalTransform>(site);
            var box = new NavBox(transform.Position, transform.Rotation, World.Get<NavObstacle>(site).Size);
            var extent = box.Extents * 2f;
            Assert.AreEqual(1.2f, extent.x, 0.001f, "the footprint turns with the segment");
            Assert.AreEqual(2f, extent.y, 0.001f);
        }

        [Test]
        public void PlaceLine_SkipsOccupiedSegments_AndChargesOnlyThePlacedOnes()
        {
            var prefab = WallPrefab();
            World.SpawnBuilding(0, new float3(6f, 0f, 0.5f), new float2(2f, 2f));
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 0.3f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(3, sites.Length);
            Assert.AreEqual(8f, sites[1].x, 1e-4f, "the blocked segment is skipped, the line goes on");
            Assert.AreEqual(700, World.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_TwoMetresFromABuilding_LosesOnlyTheSegmentsInTheGap()
        {
            var prefab = WallPrefab();
            var map = World.Singleton<MapSettings>();
            map.BuildingGap = 4f;
            World.Set(World.MatchEntity(), map);
            // Segments span z -0.1 to 1.1; the building's near side is 2 m beyond, over x 7 to 9.
            World.SpawnBuilding(0, new float3(8f, 0f, 4.1f), new float2(2f, 2f));
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(2f, 0f, 0.3f), new float3(16.2f, 0f, 0.3f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(3, sites.Length, "segments within 4 m of the building, corners included, are skipped");
            Assert.AreEqual(2f, sites[0].x, 1e-4f);
            Assert.AreEqual(14f, sites[1].x, 1e-4f);
            Assert.AreEqual(16f, sites[2].x, 1e-4f, "flush with its neighbour");
            Assert.AreEqual(700, World.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_StopsWhereTheMoneyRunsOut()
        {
            var prefab = WallPrefab(250);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 0.3f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(2, sites.Length);
            Assert.AreEqual(6f, sites[1].x, 1e-4f, "bought from the drag start");
            Assert.AreEqual(50, World.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_StopsAtTheBuildLimit()
        {
            var prefab = World.AddBuildLimit(WallPrefab(), 3);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 0.3f));
            World.Tick();

            Assert.AreEqual(3, SitePositions().Length);
            Assert.AreEqual(700, World.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_QueuesBuildOrdersFromTheNearerEnd()
        {
            var prefab = WallPrefab();
            var builder = Builder(prefab, new float3(14f, 0f, 0f));
            World.PlaceLine(builder, prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 0.3f));
            World.Tick();

            var active = World.Get<ActiveOrder>(builder).Value;
            Assert.AreEqual(OrderType.Build, active.Type);
            Assert.AreEqual(10f, active.Position.x, 1e-4f);
            var queued = World.Buffer<QueuedOrder>(builder);
            Assert.AreEqual(3, queued.Length);
            for (var i = 0; i < queued.Length; i++)
            {
                Assert.AreEqual(OrderType.Build, queued[i].Value.Type);
                Assert.AreEqual(8f - 2f * i, queued[i].Value.Position.x, 1e-4f);
            }
        }

        [Test]
        public void PlaceLine_IgnoresPrefabsNotPlacedInLines()
        {
            var prefab = WallPrefab();
            World.EntityManager.RemoveComponent<LinePlacement>(prefab);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 0.3f));
            World.Tick();

            Assert.AreEqual(0, SitePositions().Length);
        }

        [Test]
        public void CanAfford_ScalesTheSegmentCostByTheLine()
        {
            var prefab = WallPrefab(300);
            var stock = World.EntityManager.GetBuffer<ResourceStock>(World.Player(1)).AsNativeArray();
            var cost = World.Buffer<ResourceCost>(prefab);

            Assert.IsTrue(ResourceMath.CanAfford(stock, cost, 3));
            Assert.IsFalse(ResourceMath.CanAfford(stock, cost, 4));
        }

        [Test]
        public void PlaceLine_Diagonal_LaysGapFreeSegmentsYawedAlongIt()
        {
            var prefab = WallPrefab();
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 4f), new float3(12f, 0f, 12f));
            World.Tick();

            var sites = World.All<ConstructionProgress>();
            Assert.AreEqual(7, sites.Length);
            var along = math.normalize(new float2(1f, 1f));
            var boxes = new NavBox[sites.Length];
            for (var i = 0; i < sites.Length; i++)
            {
                var transform = World.Get<LocalTransform>(sites[i]);
                boxes[i] = new NavBox(transform.Position, transform.Rotation, World.Get<NavObstacle>(sites[i]).Size);
                Assert.AreEqual(1f, math.abs(math.dot(boxes[i].Right, along)), 1e-4f, "the length runs along the line");
            }

            var positions = SitePositions();
            for (var i = 1; i < positions.Length; i++)
            {
                Assert.AreEqual(2f, math.distance(positions[i - 1].xz, positions[i].xz), 1e-3f, "flush, no gap");
            }

            for (var i = 0; i < boxes.Length; i++)
            {
                for (var j = i + 1; j < boxes.Length; j++)
                {
                    Assert.IsFalse(boxes[i].Overlaps(boxes[j]), $"segments {i} and {j} only touch");
                }
            }
        }

        [Test]
        public void Matches_IgnoresHeightAndCursorDrift_ButNotAnotherSegment()
        {
            var line = Line(new float3(4f, 0f, 4f), new float3(9.2f, 0f, 4f));

            Assert.IsTrue(line.Matches(Line(new float3(4.1f, 3f, 4f), new float3(9.4f, 0f, 4.04f))), "within the snap");
            Assert.IsFalse(line.Matches(Line(new float3(4f, 0f, 4f), new float3(11f, 0f, 4f))), "a longer line");
            Assert.IsFalse(line.Matches(Line(new float3(4f, 0f, 4f), new float3(4f, 0f, 9f))), "along the other axis");
            Assert.IsFalse(line.Matches(Line(new float3(4f, 0f, 4f), new float3(9.2f, 0f, 6f))), "at another angle");
        }

        private static PlacementLine Line(float3 start, float3 end)
        {
            var map = new MapSettings { Min = new float2(-100f), Size = new float2(200f), NavCellSize = 1f };
            return new PlacementLine(map, start, end, new float2(2f, 1.2f), PlacementSurface.Land);
        }

        private struct AlongLine : IComparer<float3>
        {
            public int Compare(float3 a, float3 b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z);
        }
    }
}
