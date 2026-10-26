using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Orders
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

        private static BuildingSpot Spot(float3 position, float2 footprint, float yaw = PlacementMath.CameraYaw) =>
            new(position, yaw, footprint, PlacementSurface.Land);

        private static BuildingSpot Snapped(in MapSettings map, float3 position, float2 footprint) =>
            Spot(position, footprint).Snap(map);

        private static void AssertOnCellBoundaries(in MapSettings map, in BuildingSpot spot)
        {
            var edge = (spot.Center.xz - spot.Box.Extents - map.Min) / map.NavCellSize;
            Assert.AreEqual(math.round(edge.x), edge.x, 1e-4f, "x edge on a cell boundary");
            Assert.AreEqual(math.round(edge.y), edge.y, 1e-4f, "z edge on a cell boundary");
        }

        [Test]
        public void Snap_OddFootprint_CentresOnCellAndEdgesOnBoundaries()
        {
            var map = Map();
            var footprint = new float2(3f, 3f);
            var snapped = Snapped(in map, new float3(0.3f, 2f, 0.2f), footprint);

            Assert.AreEqual(0.5f, snapped.Center.x, 1e-4f);
            Assert.AreEqual(0.5f, snapped.Center.z, 1e-4f);
            Assert.AreEqual(2f, snapped.Center.y, "height is kept");
            AssertOnCellBoundaries(in map, snapped);
        }

        [Test]
        public void Snap_EvenAndMixedFootprints_LandEdgesOnBoundaries()
        {
            var map = Map();
            var even = new float2(2f, 4f);
            var snapped = Snapped(in map, new float3(0.3f, 0f, -0.4f), even);
            Assert.AreEqual(0f, snapped.Center.x, 1e-4f);
            Assert.AreEqual(0f, snapped.Center.z, 1e-4f);
            AssertOnCellBoundaries(in map, snapped);

            AssertOnCellBoundaries(in map, Snapped(in map, new float3(1.7f, 0f, 1.7f), new float2(2f, 3f)));

            var coarse = Map(2f);
            AssertOnCellBoundaries(in coarse, Snapped(in coarse, new float3(3.1f, 0f, -2.9f), new float2(4f, 6f)));
        }

        [Test]
        public void Snap_NearAQuarterTurn_MakesItExact_AndKeepsTheTurnedEdgesOnCells()
        {
            var map = Map();
            var nearQuarter = PlacementMath.CameraYaw + math.PI * 0.5f + math.radians(0.3f);
            var spot = Spot(new float3(1.3f, 0f, 0.6f), new float2(2f, 3f), nearQuarter).Snap(map);

            Assert.AreEqual(PlacementMath.CameraYaw + math.PI * 0.5f, spot.Yaw, 1e-6f, "the yaw is an exact quarter");
            MathAssert.Near(new float2(3f, 2f), spot.Box.Extents * 2f);
            AssertOnCellBoundaries(in map, spot);
        }

        [Test]
        public void Snap_AtAFreeYaw_KeepsTheYaw_AndCentresOnTheHalfCellLattice()
        {
            var map = Map();
            var spot = Spot(new float3(1.3f, 0f, 0.6f), new float2(2f, 3f), 0.7f).Snap(map);

            Assert.AreEqual(0.7f, spot.Yaw);
            Assert.AreEqual(1.5f, spot.Center.x, 1e-4f);
            Assert.AreEqual(0.5f, spot.Center.z, 1e-4f);
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
            Assert.IsTrue(Spot(float3.zero, footprint).IsValid(in map, in grid), "free cells");
            Assert.IsFalse(Spot(new float3(9.5f, 0f, 0f), footprint).IsValid(in map, in grid),
                "footprint crosses the map edge");
            Assert.IsFalse(Spot(new float3(50f, 0f, 50f), footprint).IsValid(in map, in grid), "outside the map");

            var blocked = grid.WorldToCell(new float3(0.5f, 0f, 0.5f));
            grid.Cells[grid.Index(blocked)] = (byte)NavSurface.Blocked;
            Assert.IsFalse(Spot(float3.zero, footprint).IsValid(in map, in grid), "blocked cell under footprint");
            Assert.IsTrue(Spot(new float3(-4f, 0f, -4f), footprint).IsValid(in map, in grid), "elsewhere is free");
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
            Assert.IsTrue(Spot(float3.zero, footprint).IsValid(in map, in grid), "the site clears a tree");

            grid.Cells[grid.Index(tree)] = (byte)NavSurface.Blocked;
            Assert.IsFalse(Spot(float3.zero, footprint).IsValid(in map, in grid), "a house's cells block");
            grid.Cells.Dispose();
            grid.Scenery.Dispose();
        }

        [Test]
        public void IsValid_ChecksTheFootprintAtItsYaw()
        {
            var map = Map();
            var footprint = new float2(2f, 6f);
            var grid = LandGrid(map);
            var house = grid.WorldToCell(new float3(2.5f, 0f, 0.5f));
            grid.Cells[grid.Index(house)] = (byte)NavSurface.Blocked;
            var quarter = math.PI * 0.5f;

            Assert.IsTrue(Spot(float3.zero, footprint).IsValid(in map, in grid), "the narrow side misses the house");
            Assert.IsFalse(Spot(float3.zero, footprint, PlacementMath.CameraYaw + quarter).IsValid(in map, in grid),
                "a quarter turn lays the long side over it");
            Assert.IsFalse(Spot(float3.zero, footprint, math.radians(60f)).IsValid(in map, in grid),
                "so does a free turn");
            grid.Cells.Dispose();
            grid.Scenery.Dispose();
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
