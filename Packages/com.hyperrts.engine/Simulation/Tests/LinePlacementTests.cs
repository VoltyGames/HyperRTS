using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Resources;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Dragged lines of wall segments: axes, skipped cells, running out mid-line and the builders' queue.</summary>
    public class LinePlacementTests
    {
        private static readonly float3 Far = new(90f, 0f, 90f);

        private TestWorld _world;
        private ResourceType _supplies;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
            _supplies = _world.CreateSupplies();
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity WallPrefab(int funds = 1000)
        {
            _world.AddResources(1, _supplies, funds);
            var prefab = _world.MakePrefab(_world.SpawnBuilding(0, Far, new float2(2f, 1.2f), name: "Wall"));
            _world.EntityManager.AddComponent<LinePlacement>(prefab);
            _world.SetCost(prefab, _supplies, 100);
            return prefab;
        }

        private Entity Builder(Entity prefab, float3 position) =>
            _world.MakeBuilder(_world.SpawnUnit(1, position), prefab);

        /// <summary>The player's sites, ordered along X then Z.</summary>
        private NativeList<float3> SitePositions()
        {
            var result = new NativeList<float3>(Allocator.Temp);
            foreach (var site in _world.All<ConstructionProgress>())
            {
                if (_world.Get<Faction>(site).Value == 1)
                {
                    result.Add(_world.Get<LocalTransform>(site).Position);
                }
            }

            result.Sort(new AlongLine());
            return result;
        }

        [Test]
        public void PlaceLine_AlongX_PlacesOneSegmentPerFootprintLength()
        {
            var prefab = WallPrefab();
            _world.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 1f));
            _world.Tick();

            var sites = SitePositions();
            Assert.AreEqual(4, sites.Length);
            for (var i = 0; i < sites.Length; i++)
            {
                Assert.AreEqual(new float3(4f + 2f * i, 0f, 0.5f), sites[i]);
            }

            Assert.AreEqual(600, _world.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_AlongZ_TurnsEachSegmentAQuarter()
        {
            var prefab = WallPrefab();
            _world.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4.3f, 0f, 4f), new float3(4.6f, 0f, 10.2f));
            _world.Tick();

            var sites = SitePositions();
            Assert.AreEqual(4, sites.Length);
            for (var i = 0; i < sites.Length; i++)
            {
                Assert.AreEqual(new float3(4.5f, 0f, 4f + 2f * i), sites[i]);
            }

            var site = _world.All<ConstructionProgress>()[0];
            var transform = _world.Get<LocalTransform>(site);
            var box = new NavBox(transform.Position, transform.Rotation, _world.Get<NavObstacle>(site).Size);
            var extent = box.Extents * 2f;
            Assert.AreEqual(1.2f, extent.x, 0.001f, "the footprint turns with the segment");
            Assert.AreEqual(2f, extent.y, 0.001f);
        }

        [Test]
        public void PlaceLine_SkipsOccupiedSegments_AndChargesOnlyThePlacedOnes()
        {
            var prefab = WallPrefab();
            _world.SpawnBuilding(0, new float3(6f, 0f, 0.5f), new float2(2f, 2f));
            _world.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 1f));
            _world.Tick();

            var sites = SitePositions();
            Assert.AreEqual(3, sites.Length);
            Assert.AreEqual(8f, sites[1].x, "the blocked segment is skipped, the line goes on");
            Assert.AreEqual(700, _world.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_StopsWhereTheMoneyRunsOut()
        {
            var prefab = WallPrefab(250);
            _world.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 1f));
            _world.Tick();

            var sites = SitePositions();
            Assert.AreEqual(2, sites.Length);
            Assert.AreEqual(6f, sites[1].x, "bought from the drag start");
            Assert.AreEqual(50, _world.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_StopsAtTheBuildLimit()
        {
            var prefab = _world.AddBuildLimit(WallPrefab(), 3);
            _world.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 1f));
            _world.Tick();

            Assert.AreEqual(3, SitePositions().Length);
            Assert.AreEqual(700, _world.GetResources(1, _supplies));
        }

        [Test]
        public void PlaceLine_QueuesBuildOrdersFromTheNearerEnd()
        {
            var prefab = WallPrefab();
            var builder = Builder(prefab, new float3(14f, 0f, 0f));
            _world.PlaceLine(builder, prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 1f));
            _world.Tick();

            var active = _world.Get<ActiveOrder>(builder).Value;
            Assert.AreEqual(OrderType.Build, active.Type);
            Assert.AreEqual(10f, active.Position.x);
            var queued = _world.EntityManager.GetBuffer<QueuedOrder>(builder);
            Assert.AreEqual(3, queued.Length);
            for (var i = 0; i < queued.Length; i++)
            {
                Assert.AreEqual(OrderType.Build, queued[i].Value.Type);
                Assert.AreEqual(8f - 2f * i, queued[i].Value.Position.x);
            }
        }

        [Test]
        public void PlaceLine_IgnoresPrefabsNotPlacedInLines()
        {
            var prefab = WallPrefab();
            _world.EntityManager.RemoveComponent<LinePlacement>(prefab);
            _world.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 0.3f), new float3(10.2f, 0f, 1f));
            _world.Tick();

            Assert.AreEqual(0, SitePositions().Length);
        }

        [Test]
        public void CanAfford_ScalesTheSegmentCostByTheLine()
        {
            var prefab = WallPrefab(300);
            var stock = _world.EntityManager.GetBuffer<ResourceStock>(_world.Player(1)).AsNativeArray();
            var cost = _world.EntityManager.GetBuffer<ResourceCost>(prefab);

            Assert.IsTrue(ResourceMath.CanAfford(stock, cost, 3));
            Assert.IsFalse(ResourceMath.CanAfford(stock, cost, 4));
        }

        [Test]
        public void Matches_IgnoresHeightAndCursorDrift_ButNotAnotherSegment()
        {
            var line = Line(new float3(4f, 0f, 4f), new float3(9.2f, 0f, 4f));

            Assert.IsTrue(line.Matches(Line(new float3(4.1f, 3f, 4f), new float3(9.4f, 0f, 4.1f))));
            Assert.IsFalse(line.Matches(Line(new float3(4f, 0f, 4f), new float3(11f, 0f, 4f))), "a longer line");
            Assert.IsFalse(line.Matches(Line(new float3(4f, 0f, 4f), new float3(4f, 0f, 9f))), "along the other axis");
        }

        private static PlacementLine Line(float3 start, float3 end)
        {
            var map = new MapSettings { Min = new float2(-100f), Size = new float2(200f), NavCellSize = 1f };
            return new PlacementLine(map, start, end, new float2(2f, 1.2f));
        }

        private struct AlongLine : IComparer<float3>
        {
            public int Compare(float3 a, float3 b) => a.x != b.x ? a.x.CompareTo(b.x) : a.z.CompareTo(b.z);
        }
    }
}
