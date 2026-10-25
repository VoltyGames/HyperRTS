using HyperRTS.Simulation.Common;
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

        /// <summary>Open land under every cell of <paramref name="map"/>, with an empty scenery layer as the nav grid builds it.</summary>
        private static NavGrid LandGrid(in MapSettings map)
        {
            var size = (int2)(map.Size / map.NavCellSize);
            var count = size.x * size.y;
            var cells = new NativeArray<byte>(count, Allocator.Temp);
            for (var i = 0; i < count; i++)
            {
                cells[i] = (byte)NavSurface.Land;
            }

            var scenery = new NativeArray<byte>(count, Allocator.Temp);
            return new NavGrid { Cells = cells, Scenery = scenery, Size = size, Min = map.Min, CellSize = map.NavCellSize };
        }

        [Test]
        public void IsValid_RejectsOutsideMapAndBlockedCells()
        {
            var map = Map();
            var footprint = new float2(2f, 2f);
            var grid = LandGrid(map);
            Assert.IsTrue(PlacementMath.IsValid(in map, in grid, float3.zero, footprint), "free cells");
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, new float3(9.5f, 0f, 0f), footprint),
                "footprint crosses the map edge");
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, new float3(50f, 0f, 50f), footprint),
                "outside the map");

            var blocked = grid.WorldToCell(new float3(0.5f, 0f, 0.5f));
            grid.Cells[grid.Index(blocked)] = (byte)NavSurface.Blocked;
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, float3.zero, footprint), "blocked cell under footprint");
            Assert.IsTrue(PlacementMath.IsValid(in map, in grid, new float3(-4f, 0f, -4f), footprint), "elsewhere is free");
            grid.Cells.Dispose();
            grid.Scenery.Dispose();
        }

        [Test]
        public void IsValid_PlacesOverOpenSceneryButNotObstacles()
        {
            var map = Map();
            var footprint = new float2(4f, 4f);
            var grid = LandGrid(map);
            var tree = grid.WorldToCell(new float3(1.5f, 0f, -1.5f));
            grid.Scenery[grid.Index(tree)] = 1;
            Assert.IsTrue(PlacementMath.IsValid(in map, in grid, float3.zero, footprint), "the site clears a tree");

            grid.Cells[grid.Index(tree)] = (byte)NavSurface.Blocked;
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, float3.zero, footprint), "a house's cells block");
            grid.Cells.Dispose();
            grid.Scenery.Dispose();
        }

        [Test]
        public void IsValid_ChecksTheFootprintAtItsTurn()
        {
            var map = Map();
            var footprint = new float2(2f, 6f);
            var grid = LandGrid(map);
            var house = grid.WorldToCell(new float3(2.5f, 0f, 0.5f));
            grid.Cells[grid.Index(house)] = (byte)NavSurface.Blocked;

            Assert.IsTrue(PlacementMath.IsValid(in map, in grid, float3.zero, PlacementMath.Turned(footprint, 0)),
                "the narrow side misses the house");
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, float3.zero, PlacementMath.Turned(footprint, 1)),
                "a quarter turn lays the long side over it");
            Assert.IsFalse(PlacementMath.IsValid(in map, in grid, float3.zero, PlacementMath.Turned(footprint, 3)));
            grid.Cells.Dispose();
            grid.Scenery.Dispose();
        }

        [Test]
        public void TurnToward_FacesTheFrontAlongTheDrag()
        {
            for (var turn = 0; turn < PlacementMath.Turns; turn++)
            {
                var front = math.rotate(PlacementMath.Facing(turn), new float3(0f, 0f, 1f)).xz;
                Assert.AreEqual(turn, PlacementMath.TurnToward(front), $"front of turn {turn}");
                var skewed = math.rotate(quaternion.RotateY(0.6f), new float3(front.x, 0f, front.y)).xz;
                Assert.AreEqual(turn, PlacementMath.TurnToward(skewed), $"off-axis drag near turn {turn}");
            }

            Assert.AreEqual(0, PlacementMath.TurnToward(new float2(0f, -1f)), "toward the camera is the default turn");
        }

        [Test]
        public void RaycastPlane_IntersectsPlaneAndRejectsParallelOrUpwardRays()
        {
            var down = math.normalize(new float3(0f, -1f, 1f));
            Assert.IsTrue(TerrainHeight.RaycastPlane(new float3(0f, 10f, 0f), down, 0f, out var point));
            Assert.AreEqual(0f, point.x, 1e-4f);
            Assert.AreEqual(0f, point.y, 1e-4f);
            Assert.AreEqual(10f, point.z, 1e-4f);

            Assert.IsTrue(TerrainHeight.RaycastPlane(new float3(0f, 10f, 0f), down, 2f, out var raised));
            Assert.AreEqual(8f, raised.z, 1e-4f);

            Assert.IsFalse(TerrainHeight.RaycastPlane(new float3(0f, 10f, 0f), new float3(1f, 0f, 0f), 0f, out _));
            Assert.IsFalse(TerrainHeight.RaycastPlane(new float3(0f, 10f, 0f), new float3(0f, 1f, 0f), 0f, out _));
        }

        [Test]
        public void Raycast_WithoutHeightfield_HitsFlatGroundWithinRange()
        {
            var terrain = default(TerrainHeight);
            var down = math.normalize(new float3(0f, -1f, 1f));
            Assert.IsTrue(terrain.Raycast(new float3(0f, 10f, 0f), down, 20f, out var point));
            Assert.AreEqual(0f, point.y, 1e-4f);
            Assert.AreEqual(10f, point.z, 1e-4f);

            Assert.IsFalse(terrain.Raycast(new float3(0f, 10f, 0f), down, 10f, out _), "ground beyond max distance");
            Assert.IsFalse(terrain.Raycast(new float3(0f, 10f, 0f), new float3(0f, 1f, 0f), 20f, out _), "upward ray");
        }
    }
}
