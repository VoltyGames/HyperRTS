using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Death;
using HyperRTS.Simulation.Destruction;
using HyperRTS.Simulation.Movement.Crushing;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Areas;
using HyperRTS.Simulation.Navigation.Grid;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Grid
{
    /// <summary>
    /// Scenery and wrecks on the ground: trucks go round woods, tanks crush through, and a wreck closes its land
    /// cells until it is buried.
    /// </summary>
    public class NavSceneryTests : SimulationFixture
    {
        private const int TreeCrush = 3;
        private const float HalfLine = 10f;
        private const float WreckRadius = 1.85f;
        private const float WreckLength = 6.9f;
        private const float UnitRadius = 0.5f;

        // A unit's clearance from closed cells is a little under its radius.
        private const float Reach = WreckRadius + UnitRadius - 0.1f;

        // A tree in every cell along Z at x 0, from z -10 to 10.
        private Entity[] PlantTreeLine()
        {
            var trees = new Entity[(int)(HalfLine * 2f) + 1];
            for (var i = 0; i < trees.Length; i++)
            {
                trees[i] = PlantTree(new float3(0.5f, 0f, i - HalfLine + 0.5f));
            }

            World.Tick();
            return trees;
        }

        private Entity PlantTree(float3 position)
        {
            var tree = World.EntityManager.CreateEntity();
            World.Add(tree, LocalTransform.FromPosition(position));
            var writer = World.Writer(tree);
            DestructibleAuthoring.AddScenery(writer, 50f, TreeCrush, Entity.Null, float2.zero,
                SceneryId.At(position, quaternion.identity));
            return tree;
        }

        private Entity SendAcross(byte crushLimit, float radius = 0.5f)
        {
            var unit = World.SpawnUnit(1, new float3(-5f, 0f, 0.5f), radius: radius);
            World.Set(unit, new NavAgent { Radius = radius, SceneryCrushLimit = crushLimit, Mass = 1f });
            World.MoveTo(unit, new float3(5f, 0f, 0.5f));
            return unit;
        }

        private int Standing(Entity[] trees)
        {
            var count = 0;
            foreach (var tree in trees)
            {
                count += World.EntityManager.Exists(tree) ? 1 : 0;
            }

            return count;
        }

        /// <summary>Runs the drive and returns how far off the straight line the unit went.</summary>
        private float Drive(Entity unit, bool mayEnterTrees)
        {
            var widest = 0f;
            for (var t = 0f; t < 12f; t += TestWorld.FrameTime)
            {
                World.Tick();
                var position = World.GetPosition(unit);
                widest = math.max(widest, math.abs(position.z - 0.5f));
                var grid = World.Grid();
                var scenery = grid.Scenery[grid.Grid.Index(grid.Grid.Cell(position))];
                Assert.IsTrue(mayEnterTrees || scenery == 0, $"drove into a tree at {position}");
            }

            Assert.Less(math.distance(World.GetPosition(unit).xz, new float2(5f, 0.5f)), 1f, "arrived");
            return widest;
        }

        [Test]
        public void Truck_RoutesRoundATreeLine_AndNeverEntersIt()
        {
            var trees = PlantTreeLine();
            var truck = SendAcross(crushLimit: 1);

            Assert.Greater(Drive(truck, mayEnterTrees: false), HalfLine, "went round the line's end");
            Assert.AreEqual(trees.Length, Standing(trees));
        }

        [Test]
        public void Tank_CrushesStraightThrough_WithoutInvalidatingPaths()
        {
            var trees = PlantTreeLine();
            var tank = SendAcross(crushLimit: TreeCrush);
            World.Add(tank, new Crusher { Size = TreeCrush });
            var version = World.Grid().Version;

            Assert.Less(Drive(tank, mayEnterTrees: true), 1f, "drove straight");
            Assert.Less(Standing(trees), trees.Length, "and crushed its way through");
            Assert.AreEqual(version, World.Grid().Version, "felled scenery only opens cells");
        }

        [Test]
        public void LongTruck_WithATreeUnderItsHull_DrivesOffItToTheGoal()
        {
            // Between where the old end and centre circles sat, so the rear end met the tree once moving.
            PlantTree(new float3(-1.5f, 0f, 0.5f));
            World.Tick();
            var truck = World.SpawnUnit(1, new float3(0f, 0f, 0.5f), radius: 1f);
            World.Set(truck, new NavAgent
            {
                Radius = 1f, HullLength = 10f, SceneryCrushLimit = 1, Mass = 17f,
            });
            World.Set(truck,
                LocalTransform.FromPositionRotation(new float3(0f, 0f, 0.5f), quaternion.LookRotationSafe(math.right(), math.up())));
            var goal = new float3(20f, 0f, 0.5f);
            World.MoveTo(truck, goal);

            World.Run(8f);

            Assert.Less(math.distance(World.GetPosition(truck).xz, goal.xz), 1f, "never wedged on the tree");
        }

        [Test]
        public void Infantry_WalkBetweenTrees_AndLeaveThemStanding()
        {
            var trees = PlantTreeLine();
            var rifleman = SendAcross(NavAccess.AllScenery, radius: 0.4f);

            Assert.Less(Drive(rifleman, mayEnterTrees: true), 1f);
            Assert.AreEqual(trees.Length, Standing(trees));
        }

        [Test]
        public void OpenObstacle_StampsItsWholeBoxAsScenery_OrNothingWithoutARating()
        {
            var trap = World.SpawnBuilding(1, new float3(0.5f, 0f, 0.5f), new float2(4f, 1f));
            World.Set(trap, new NavObstacle { Size = new float2(4f, 1f), Open = true });
            World.Add(trap, new Destructible { Crush = 4 });
            var trench = World.SpawnBuilding(1, new float3(0.5f, 0f, 10.5f), new float2(4f, 1f));
            World.Set(trench, new NavObstacle { Size = new float2(4f, 1f), Open = true });
            World.Tick();

            var grid = World.Grid();
            Assert.AreEqual(4, grid.Scenery[grid.Grid.Index(grid.Grid.Cell(new float3(-1f, 0f, 0.5f)))], "the end cells too");
            Assert.AreEqual(4, grid.Scenery[grid.Grid.Index(grid.Grid.Cell(new float3(2f, 0f, 0.5f)))]);
            Assert.AreEqual(0, grid.Scenery[grid.Grid.Index(grid.Grid.Cell(new float3(0.5f, 0f, 2.5f)))]);
            Assert.IsTrue(grid.IsWalkable(new float3(0.5f, 0f, 0.5f)), "infantry walk through, a tank's limit is below the rating");
            Assert.AreEqual(0, grid.Scenery[grid.Grid.Index(grid.Grid.Cell(new float3(0.5f, 0f, 10.5f)))], "a trench has no rating");
            Assert.IsTrue(grid.IsWalkable(new float3(0.5f, 0f, 10.5f)));
        }

        // Lies crosswise along Z at the origin, so a unit driving along X meets its long side.
        private Entity SpawnWreck()
        {
            var wreck = World.EntityManager.CreateEntity();
            World.Add(wreck, LocalTransform.FromPosition(float3.zero));
            var writer = World.Writer(wreck);
            BlockerAuthoring.Add(writer, "wreck", WreckRadius, WreckLength);
            return wreck;
        }

        private Entity DriveAcross(float3 start)
        {
            var unit = World.SpawnUnit(1, start, radius: UnitRadius);
            World.Set(unit, new MoveDestination { Value = new float3(10f, 0f, start.z) });
            World.SetEnabled<MoveDestination>(unit, true);
            return unit;
        }

        private float Gap(Entity unit)
        {
            var half = WreckLength * 0.5f - WreckRadius;
            var position = World.GetPosition(unit);
            return math.distance(position.xz, new float2(0f, math.clamp(position.z, -half, half)));
        }

        [Test]
        public void UnitDrivingAtWreck_GoesRound_NeverEnteringIt()
        {
            var wreck = SpawnWreck();
            var unit = DriveAcross(new float3(-10f, 0f, 1f));

            for (var frame = 0; frame < 300; frame++)
            {
                World.Tick();
                Assert.GreaterOrEqual(Gap(unit), Reach, $"inside the wreck at frame {frame}");
            }

            Assert.AreEqual(10f, World.GetPosition(unit).x, 0.5f, "it went round to the goal");
            Assert.AreEqual(float3.zero, World.GetPosition(wreck), "the wreck never moves");
        }

        [Test]
        public void UnitCaughtByANewWreck_DrivesOut()
        {
            SpawnWreck();
            var unit = DriveAcross(new float3(1f, 0f, 0.5f));

            World.Run(6f);

            Assert.AreEqual(10f, World.GetPosition(unit).x, 0.5f, "it backed out of the closed cells and went on");
        }

        [Test]
        public void BuriedWreck_OpensItsCells()
        {
            var wreck = SpawnWreck();
            var writer = World.Writer(wreck);
            DecayAuthoring.Add(writer, "wreck", 2.5f, 30f, 3f);
            World.Tick();
            Assert.IsFalse(World.Grid().IsWalkable(float3.zero), "it blocks while it waits");

            World.Run(3f);
            Assert.IsFalse(World.IsEnabled<Blocker>(wreck), "it stops blocking once it is buried");
            Assert.IsTrue(World.Grid().IsWalkable(float3.zero), "its cells open again");

            var unit = DriveAcross(new float3(-10f, 0f, 0f));
            for (var frame = 0; frame < 150; frame++)
            {
                World.Tick();
                Assert.AreEqual(0f, World.GetPosition(unit).z, 0.2f, $"left the straight line at frame {frame}");
            }

            Assert.AreEqual(10f, World.GetPosition(unit).x, 0.5f, "the unit drove straight through");
        }
    }
}
