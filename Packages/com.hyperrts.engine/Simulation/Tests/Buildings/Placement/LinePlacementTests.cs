using HyperRTS.Simulation.Buildings.Placement;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Resources;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Buildings.Placement
{
    /// <summary>
    /// How one line lays its segments: along an axis or a diagonal, round what is in the way, by the building gap, and
    /// as far as money and the build limit go.
    /// </summary>
    public class LinePlacementTests : WallFixture
    {
        [Test]
        public void AlongX_LaysOneSegmentPerFootprintLength_FacingTheCamera()
        {
            var prefab = WallPrefab();
            World.PlaceLine(Builder(prefab, float3.zero), prefab, LegStart, LegEnd);
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(4, sites.Length);
            for (var i = 0; i < sites.Length; i++)
            {
                MathAssert.Near(new float3(4f + 2f * i, 0f, 0.5f), sites[i]);
            }

            Assert.AreEqual(600, World.GetResources(1, Supplies));
            var rotation = World.Get<LocalTransform>(World.All<ConstructionProgress>()[0]).Rotation;
            var front = math.rotate(rotation, new float3(0f, 0f, 1f));
            var expected = math.rotate(quaternion.RotateY(PlacementMath.CameraYaw), new float3(0f, 0f, 1f));
            MathAssert.Near(expected.xz, front.xz, 0.001f);
        }

        [Test]
        public void AlongZ_TurnsEachSegmentAQuarter()
        {
            var prefab = WallPrefab();
            var builder = Builder(prefab, float3.zero);
            World.PlaceLine(builder, prefab, new float3(4.3f, 0f, 4f), new float3(4.32f, 0f, 10.2f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(4, sites.Length);
            for (var i = 0; i < sites.Length; i++)
            {
                MathAssert.Near(new float3(4.5f, 0f, 4f + 2f * i), sites[i]);
            }

            var extent = SiteBoxes()[0].Extents * 2f;
            Assert.AreEqual(1.2f, extent.x, 0.001f, "the footprint turns with the segment");
            Assert.AreEqual(2f, extent.y, 0.001f);
        }

        [Test]
        public void Diagonal_LaysFlushSegmentsYawedAlongIt()
        {
            var prefab = WallPrefab();
            World.PlaceLine(Builder(prefab, float3.zero), prefab, new float3(4f, 0f, 4f), new float3(12f, 0f, 12f));
            World.Tick();

            var boxes = SiteBoxes();
            Assert.AreEqual(7, boxes.Length);
            var along = math.normalize(new float2(1f, 1f));
            foreach (var box in boxes)
            {
                Assert.AreEqual(1f, math.abs(math.dot(box.Right, along)), 1e-4f, "the length runs along the line");
            }

            var positions = SitePositions();
            for (var i = 1; i < positions.Length; i++)
            {
                Assert.AreEqual(2f, math.distance(positions[i - 1].xz, positions[i].xz), 1e-3f, "flush, no gap");
            }

            AssertOnlyTouch(boxes);
        }

        [Test]
        public void SkipsOccupiedSegments_AndChargesOnlyThePlacedOnes()
        {
            var prefab = WallPrefab();
            World.SpawnBuilding(0, new float3(6f, 0f, 0.5f), new float2(2f, 2f));
            World.PlaceLine(Builder(prefab, float3.zero), prefab, LegStart, LegEnd);
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(3, sites.Length);
            Assert.AreEqual(8f, sites[1].x, 1e-4f, "the blocked segment is skipped, the line goes on");
            Assert.AreEqual(700, World.GetResources(1, Supplies));
        }

        [Test]
        public void TwoMetresFromABuilding_LosesOnlyTheSegmentsInTheGap()
        {
            var prefab = WallPrefab();
            SetBuildingGap(4f);
            // Segments span z -0.1 to 1.1; the building's near side is 2 m beyond, over x 7 to 9.
            World.SpawnBuilding(0, new float3(8f, 0f, 4.1f), new float2(2f, 2f));
            var builder = Builder(prefab, float3.zero);
            World.PlaceLine(builder, prefab, new float3(2f, 0f, 0.3f), new float3(16.2f, 0f, 0.3f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(3, sites.Length, "segments within 4 m of the building, corners included, are skipped");
            Assert.AreEqual(2f, sites[0].x, 1e-4f);
            Assert.AreEqual(14f, sites[1].x, 1e-4f);
            Assert.AreEqual(16f, sites[2].x, 1e-4f, "flush with its neighbour");
            Assert.AreEqual(700, World.GetResources(1, Supplies));
        }

        [Test]
        public void SegmentsShorterThanTheBuildingGap_LayEveryOne()
        {
            var prefab = WallPrefab();
            SetBuildingGap(4f);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, LegStart, new float3(14.2f, 0f, 0.3f));
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(6, sites.Length, "the gap keeps lanes to other buildings, not between a line's segments");
            for (var i = 0; i < sites.Length; i++)
            {
                Assert.AreEqual(4f + 2f * i, sites[i].x, 1e-4f);
            }
        }

        [Test]
        public void BesideAnotherWall_KeepsNoGapFromIt()
        {
            var prefab = WallPrefab();
            SetBuildingGap(4f);
            var builder = Builder(prefab, float3.zero);
            World.PlaceLine(builder, prefab, LegStart, LegEnd);
            World.PlaceLine(builder, prefab, LegStart + new float3(0f, 0f, 2f), LegEnd + new float3(0f, 0f, 2f),
                queue: true);
            World.Tick();

            Assert.AreEqual(8, SitePositions().Length, "walls are barriers, so two may run a metre apart");
        }

        [Test]
        public void StopsWhereTheMoneyRunsOut()
        {
            var prefab = WallPrefab(250);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, LegStart, LegEnd);
            World.Tick();

            var sites = SitePositions();
            Assert.AreEqual(2, sites.Length);
            Assert.AreEqual(6f, sites[1].x, 1e-4f, "bought from the drag start");
            Assert.AreEqual(50, World.GetResources(1, Supplies));
        }

        [Test]
        public void StopsAtTheBuildLimit()
        {
            var prefab = World.AddBuildLimit(WallPrefab(), 3);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, LegStart, LegEnd);
            World.Tick();

            Assert.AreEqual(3, SitePositions().Length);
            Assert.AreEqual(700, World.GetResources(1, Supplies));
        }

        [Test]
        public void IgnoresPrefabsNotPlacedInLines()
        {
            var prefab = WallPrefab();
            World.EntityManager.RemoveComponent<LinePlacement>(prefab);
            World.PlaceLine(Builder(prefab, float3.zero), prefab, LegStart, LegEnd);
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
            return new PlacementLine(map, start, end, Footprint, PlacementSurface.Land, LineStart.Snapped);
        }
    }
}
