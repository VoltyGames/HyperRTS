using HyperRTS.Simulation.Interaction;
using HyperRTS.Simulation.Match;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Placement rules and the pure helpers the input layer uses to build commands.</summary>
    public class InteractionTests
    {
        private static MapSettings Map(float cellSize = 1f) => new()
        {
            Min = new float2(-10f, -10f),
            Size = new float2(20f, 20f),
            NavCellSize = cellSize,
        };

        private static void AssertOnCellBoundaries(in MapSettings map, float3 center, float2 footprint)
        {
            var edge = (center.xz - footprint * 0.5f - map.Min) / map.NavCellSize;
            Assert.AreEqual(math.round(edge.x), edge.x, 1e-4f, "x edge on a cell boundary");
            Assert.AreEqual(math.round(edge.y), edge.y, 1e-4f, "z edge on a cell boundary");
        }

        [Test]
        public void Snap_OddFootprint_CentresOnCellAndEdgesOnBoundaries()
        {
            var map = Map();
            var footprint = new float2(3f, 3f);
            var snapped = PlacementMath.Snap(in map, new float3(0.3f, 2f, 0.2f), footprint);

            Assert.AreEqual(0.5f, snapped.x, 1e-4f);
            Assert.AreEqual(0.5f, snapped.z, 1e-4f);
            Assert.AreEqual(2f, snapped.y, "height is kept");
            AssertOnCellBoundaries(in map, snapped, footprint);
        }

        [Test]
        public void Snap_EvenAndMixedFootprints_LandEdgesOnBoundaries()
        {
            var map = Map();
            var even = new float2(2f, 4f);
            var snapped = PlacementMath.Snap(in map, new float3(0.3f, 0f, -0.4f), even);
            Assert.AreEqual(0f, snapped.x, 1e-4f);
            Assert.AreEqual(0f, snapped.z, 1e-4f);
            AssertOnCellBoundaries(in map, snapped, even);

            var mixed = new float2(2f, 3f);
            AssertOnCellBoundaries(in map, PlacementMath.Snap(in map, new float3(1.7f, 0f, 1.7f), mixed), mixed);

            var coarse = Map(2f);
            var large = new float2(4f, 6f);
            AssertOnCellBoundaries(in coarse, PlacementMath.Snap(in coarse, new float3(3.1f, 0f, -2.9f), large), large);
        }

        [Test]
        public void IsValid_RejectsOutsideMapAndBlockedCells()
        {
            var map = Map();
            var footprint = new float2(2f, 2f);
            var noGrid = default(NavGrid);

            Assert.IsTrue(PlacementMath.IsValid(in map, in noGrid, float3.zero, footprint), "inside, no grid");
            Assert.IsFalse(PlacementMath.IsValid(in map, in noGrid, new float3(9.5f, 0f, 0f), footprint),
                "footprint crosses the map edge");
            Assert.IsFalse(PlacementMath.IsValid(in map, in noGrid, new float3(50f, 0f, 50f), footprint),
                "outside the map");

            var cells = new NativeArray<byte>(400, Allocator.Temp);
            var grid = new NavGrid { Cells = cells, Size = new int2(20, 20), Min = map.Min, CellSize = 1f };
            Assert.IsTrue(PlacementMath.IsValid(in map, in grid, float3.zero, footprint), "free cells");

            var blocked = grid.WorldToCell(new float3(0.5f, 0f, 0.5f));
            cells[grid.Index(blocked)] = 1;
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, float3.zero, footprint), "blocked cell under footprint");
            Assert.IsTrue(PlacementMath.IsValid(in map, in grid, new float3(-4f, 0f, -4f), footprint), "elsewhere is free");
            cells.Dispose();
        }

        [Test]
        public void TryGroundPoint_IntersectsPlaneAndRejectsParallelOrUpwardRays()
        {
            var down = math.normalize(new float3(0f, -1f, 1f));
            Assert.IsTrue(CommandMath.TryGroundPoint(new float3(0f, 10f, 0f), down, 0f, out var point));
            Assert.AreEqual(0f, point.x, 1e-4f);
            Assert.AreEqual(0f, point.y, 1e-4f);
            Assert.AreEqual(10f, point.z, 1e-4f);

            Assert.IsTrue(CommandMath.TryGroundPoint(new float3(0f, 10f, 0f), down, 2f, out var raised));
            Assert.AreEqual(8f, raised.z, 1e-4f);

            Assert.IsFalse(CommandMath.TryGroundPoint(new float3(0f, 10f, 0f), new float3(1f, 0f, 0f), 0f, out _));
            Assert.IsFalse(CommandMath.TryGroundPoint(new float3(0f, 10f, 0f), new float3(0f, 1f, 0f), 0f, out _));
        }
    }
}
