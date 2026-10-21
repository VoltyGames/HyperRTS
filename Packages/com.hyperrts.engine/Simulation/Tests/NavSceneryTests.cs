using HyperRTS.Simulation.Combat;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Destructible scenery blocks the units that can't crush it: trucks go round woods, tanks crush through.</summary>
    public class NavSceneryTests
    {
        private const int TreeCrush = 3;
        private const float HalfLine = 10f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // A tree in every cell along Z at x 0, from z -10 to 10.
        private Entity[] PlantTreeLine()
        {
            var trees = new Entity[(int)(HalfLine * 2f) + 1];
            for (var i = 0; i < trees.Length; i++)
            {
                trees[i] = PlantTree(new float3(0.5f, 0f, i - HalfLine + 0.5f));
            }

            _world.Tick();
            return trees;
        }

        private Entity PlantTree(float3 position)
        {
            var tree = _world.EntityManager.CreateEntity();
            _world.EntityManager.AddComponentData(tree, LocalTransform.FromPosition(position));
            var writer = new EntityManagerWriter(_world.EntityManager, tree);
            DestructibleSetup.AddScenery(ref writer, 50f, TreeCrush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
            return tree;
        }

        private Entity SendAcross(byte sceneryPass, float radius = 0.5f)
        {
            var unit = _world.SpawnUnit(1, new float3(-5f, 0f, 0.5f), radius: radius);
            _world.EntityManager.SetComponentData(unit, new NavAgent { Radius = radius, SceneryPass = sceneryPass });
            _world.MoveTo(unit, new float3(5f, 0f, 0.5f));
            return unit;
        }

        private int Standing(Entity[] trees)
        {
            var count = 0;
            foreach (var tree in trees)
            {
                count += _world.EntityManager.Exists(tree) ? 1 : 0;
            }

            return count;
        }

        /// <summary>Runs the drive and returns how far off the straight line the unit went.</summary>
        private float Drive(Entity unit, bool mayEnterTrees)
        {
            var widest = 0f;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var position = _world.GetPosition(unit);
                widest = math.max(widest, math.abs(position.z - 0.5f));
                var grid = _world.Grid();
                var scenery = grid.Scenery[grid.Index(grid.WorldToCell(position))];
                Assert.IsTrue(mayEnterTrees || scenery == 0, $"drove into a tree at {position}");
            }

            Assert.Less(math.distance(_world.GetPosition(unit).xz, new float2(5f, 0.5f)), 1f, "arrived");
            return widest;
        }

        [Test]
        public void Trees_StampTheirCrushRating_OnTheSceneryLayer()
        {
            PlantTreeLine();
            var grid = _world.Grid();

            Assert.AreEqual(TreeCrush, grid.Scenery[grid.Index(grid.WorldToCell(new float3(0.5f, 0f, 3.5f)))]);
            Assert.AreEqual(0, grid.Scenery[grid.Index(grid.WorldToCell(new float3(1.5f, 0f, 3.5f)))]);
            Assert.IsTrue(grid.IsWalkable(new float3(0.5f, 0f, 3.5f)), "scenery leaves the cell open to the layer");
        }

        [Test]
        public void Truck_RoutesRoundATreeLine_AndNeverEntersIt()
        {
            var trees = PlantTreeLine();
            var truck = SendAcross(sceneryPass: 1);

            Assert.Greater(Drive(truck, mayEnterTrees: false), HalfLine, "went round the line's end");
            Assert.AreEqual(trees.Length, Standing(trees));
        }

        [Test]
        public void Tank_CrushesStraightThrough_WithoutInvalidatingPaths()
        {
            var trees = PlantTreeLine();
            var tank = SendAcross(sceneryPass: TreeCrush);
            _world.EntityManager.AddComponentData(tank, new Crusher { Size = TreeCrush });
            var version = _world.Grid().Version;

            Assert.Less(Drive(tank, mayEnterTrees: true), 1f, "drove straight");
            Assert.Less(Standing(trees), trees.Length, "and crushed its way through");
            Assert.AreEqual(version, _world.Grid().Version, "felled scenery only opens cells");
        }

        [Test]
        public void LongTruck_WithATreeUnderItsHull_DrivesOffItToTheGoal()
        {
            // Between where the old end and centre circles sat, so the rear end met the tree once moving.
            PlantTree(new float3(-1.5f, 0f, 0.5f));
            _world.Tick();
            var truck = _world.SpawnUnit(1, new float3(0f, 0f, 0.5f), radius: 1f);
            _world.EntityManager.SetComponentData(truck, new NavAgent { Radius = 1f, HullLength = 10f, SceneryPass = 1 });
            _world.EntityManager.SetComponentData(truck,
                LocalTransform.FromPositionRotation(new float3(0f, 0f, 0.5f), quaternion.LookRotationSafe(math.right(), math.up())));
            var goal = new float3(20f, 0f, 0.5f);
            _world.MoveTo(truck, goal);

            _world.Run(8f);

            Assert.Less(math.distance(_world.GetPosition(truck).xz, goal.xz), 1f, "never wedged on the tree");
        }

        [Test]
        public void Infantry_WalkBetweenTrees_AndLeaveThemStanding()
        {
            var trees = PlantTreeLine();
            var rifleman = SendAcross(NavPass.AllScenery, radius: 0.4f);

            Assert.Less(Drive(rifleman, mayEnterTrees: true), 1f);
            Assert.AreEqual(trees.Length, Standing(trees));
        }
    }
}
