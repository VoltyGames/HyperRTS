using System;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Navigation.Steering
{
    /// <summary>
    /// Capsule footprints and spacing: hulls keep their gap side by side and nose to tail, close at most half the room
    /// between them, slide along walls, and formations give each unit a slot its own width on open ground.
    /// </summary>
    public class HullSpacingTests
    {
        private const float TankRadius = 1.85f;
        private const float TankLength = 6.9f;
        private const float Spacing = 0.6f;

        private TestWorld _world;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(1, 2);
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnTank(float3 position, float3 facing)
        {
            var tank = _world.SpawnUnit(1, position, radius: TankRadius);
            _world.EntityManager.SetComponentData(tank, new NavAgent
            {
                Radius = TankRadius, HullLength = TankLength, Spacing = Spacing,
            });
            var transform = _world.Get<LocalTransform>(tank);
            transform.Rotation = quaternion.LookRotationSafe(facing, math.up());
            _world.EntityManager.SetComponentData(tank, transform);
            return tank;
        }

        private bool Arrived(Entity unit) => !_world.IsEnabled<ActiveOrder>(unit);

        // Selects the units and gives one move, as a player's right-click does.
        private void OrderFormation(float3 goal, params Entity[] units)
        {
            foreach (var unit in units)
            {
                _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Position = goal });
        }

        [Test]
        public void ParallelTanks_EndSideBySide_WidthPlusSpacingApart()
        {
            var left = SpawnTank(float3.zero, math.forward());
            var right = SpawnTank(new float3(1f, 0f, 0.5f), math.forward());

            _world.Run(2f);

            var gap = _world.GetPosition(right) - _world.GetPosition(left);
            Assert.AreEqual(TankRadius * 2f + Spacing, gap.x, 0.05f, "pushed apart sideways");
            Assert.AreEqual(0.5f, gap.z, 0.05f, "never pushed along the hull");
        }

        [Test]
        public void TanksInLine_EndNoseToTail_LengthPlusSpacingApart()
        {
            var rear = SpawnTank(float3.zero, math.forward());
            var front = SpawnTank(new float3(0f, 0f, 3f), math.forward());

            _world.Run(2f);

            var gap = _world.GetPosition(front) - _world.GetPosition(rear);
            Assert.AreEqual(TankLength + Spacing, gap.z, 0.05f, "pushed apart along the hulls");
            Assert.AreEqual(0f, gap.x, 0.05f);
        }

        [Test]
        public void RoundUnits_WithoutHullOrSpacing_TouchEdgeToEdge()
        {
            var a = _world.SpawnUnit(1, float3.zero);
            var b = _world.SpawnUnit(1, new float3(0.4f, 0f, 0f));

            _world.Run(1f);

            Assert.AreEqual(1f, math.distance(_world.GetPosition(a).xz, _world.GetPosition(b).xz), 0.05f);
        }

        [Test]
        public void TankColumn_MoveOrder_CompletesWithoutHullsOverlapping()
        {
            var tanks = new Entity[4];
            for (var i = 0; i < tanks.Length; i++)
            {
                tanks[i] = SpawnTank(new float3(-30f, 0f, i * 8f - 12f), new float3(1f, 0f, 0f));
            }

            OrderFormation(new float3(20f, 0f, 0f), tanks);
            // The command only starts the orders on the next tick.
            _world.Tick();
            _world.RunUntil(() => Array.TrueForAll(tanks, Arrived), 25f);

            for (var i = 0; i < tanks.Length; i++)
            {
                Assert.IsFalse(_world.IsEnabled<ActiveOrder>(tanks[i]), "every move order completed");
                for (var j = i + 1; j < tanks.Length; j++)
                {
                    var apart = math.distance(_world.GetPosition(tanks[i]).xz, _world.GetPosition(tanks[j]).xz);
                    Assert.Greater(apart, TankRadius * 2f, "hulls overlap");
                }
            }
        }

        [Test]
        public void Formation_SpreadsGroupAroundGoal_WithoutStacking()
        {
            var units = new Entity[9];
            for (var i = 0; i < units.Length; i++)
            {
                units[i] = _world.SpawnUnit(1, new float3(-10f + i % 3 * 1.2f, 0f, i / 3 * 1.2f));
            }

            var goal = new float3(10f, 0f, 0f);
            OrderFormation(goal, units);
            bool Settled(Entity unit) => Arrived(unit) && math.distance(_world.GetPosition(unit).xz, goal.xz) < 4f;
            _world.RunUntil(() => Array.TrueForAll(units, Settled), 10f);

            for (var i = 0; i < units.Length; i++)
            {
                var position = _world.GetPosition(units[i]);
                Assert.IsFalse(_world.IsEnabled<ActiveOrder>(units[i]), "every move order completed");
                Assert.Less(math.distance(position.xz, goal.xz), 4f);
                for (var j = i + 1; j < units.Length; j++)
                {
                    Assert.Greater(math.distance(position.xz, _world.GetPosition(units[j]).xz), 0.9f, "units stacked");
                }
            }
        }

        [Test]
        public void Formation_IntoABuilding_PutsEverySlotOnOpenGround()
        {
            var goal = new float3(10f, 0f, 0f);
            _world.SpawnBuilding(2, goal, new float2(8f, 8f));
            _world.Tick();
            var units = new Entity[4];
            for (var i = 0; i < units.Length; i++)
            {
                units[i] = _world.SpawnUnit(1, new float3(-10f, 0f, i * 1.5f));
            }

            // A frame first registers their movement class, as a prefab's is before any unit spawns.
            _world.Tick();
            OrderFormation(goal, units);
            _world.Tick();

            foreach (var unit in units)
            {
                var slot = _world.Get<ActiveOrder>(unit).Value.Position;
                Assert.IsTrue(_world.Grid().IsWalkable(slot), "the slot moved out of the building");
            }
        }

        [Test]
        public void MixedRow_GivesEachUnitASlotItsOwnWidth()
        {
            using var positions = new NativeArray<float3>(new[]
            {
                new float3(-2f, 0f, 0f), new float3(0f, 0f, 0f), new float3(2f, 0f, 0f),
            }, Allocator.Temp);
            using var spacings = new NativeArray<float2>(new[]
            {
                new float2(5f, 8f), new float2(1f, 1f), new float2(1f, 1f),
            }, Allocator.Temp);
            using var slots = new NativeArray<float3>(3, Allocator.Temp);

            Formation.Assign(positions, new float3(0f, 0f, 10f), spacings, slots);

            Assert.AreEqual(1f, math.distance(slots[1], slots[2]), 1e-4f, "infantry stand an infantry width apart");
            Assert.AreEqual(3f, math.distance(slots[0], slots[1]), 1e-4f, "the tank keeps half its own width");
            Assert.AreEqual(10f, slots[0].z, 1e-4f, "one row on the goal");
            Assert.Less(slots[0].x, slots[1].x, "units keep their places left to right");
        }

        [Test]
        public void HullRoom_CutsAMoveTowardANeighbourToHalfTheRoom_AndKeepsOneAwayOrAlongIt()
        {
            var room = default(HullRoom);
            room.Add(new float2(-1f, 0f), 0.4f);
            var move = room.Within(new float2(0.5f, 0.1f));
            Assert.AreEqual(0.2f, move.x, 1e-5f);
            Assert.AreEqual(0.1f, move.y, 1e-5f, "the move along the neighbour stands");

            var touching = default(HullRoom);
            touching.Add(new float2(-1f, 0f), 0f);
            Assert.AreEqual(new float2(-0.3f, 0.2f), touching.Within(new float2(-0.3f, 0.2f)), "away or along");
        }

        [Test]
        public void HullRoom_SqueezedBetweenTwoTouchingNeighbours_SlidesOutSideways()
        {
            var room = default(HullRoom);
            room.Add(new float2(-1f, 0f), 0f);
            room.Add(new float2(1f, 0f), 0f);
            var move = room.Within(new float2(0.15f, 0.05f));
            Assert.AreEqual(0f, move.x, 1e-5f);
            Assert.AreEqual(0.05f, move.y, 1e-5f);
        }

        [Test]
        public void HullRoom_AlreadyOverlapping_MayNotCloseFurther()
        {
            var room = default(HullRoom);
            room.Add(new float2(0f, 1f), -0.2f);
            Assert.IsFalse(room.Keeps(new float2(0f, -0.01f)));
            Assert.IsTrue(room.Keeps(new float2(0.1f, 0f)));
        }

        [Test]
        public void HullRoom_InACornerNoCutFits_Stands()
        {
            var room = default(HullRoom);
            room.Add(math.normalize(new float2(-1f, 1f)), 0f);
            room.Add(math.normalize(new float2(-1f, -1f)), 0f);
            var move = room.Within(new float2(0.2f, 0f));
            Assert.IsTrue(room.Keeps(move), $"cut to {move}");
            Assert.LessOrEqual(move.x, 1e-5f);
        }

        [Test]
        public void Constrain_KeepsTheCapsuleNoseOffAWall_AndSlidesAlongIt()
        {
            _world.AddAgentPrefabs(new NavAgent { Radius = 0.5f });
            _world.SpawnObstacle(new float3(2.5f, 0f, 0f), new float2(1f, 20f));
            _world.Tick();
            var access = new NavAgent { Radius = 0.5f, HullLength = 4f }.Access(_world.Grid());
            var capsule = new NavCapsule(access, new float2(1.5f, 0f));
            var from = new float2(-0.5f, 0.5f);

            Assert.Less(math.distance(from, capsule.Constrain(from, new float2(0.2f, 0.5f))), 1e-3f,
                "the centre is open, but the nose would enter the wall");
            var slid = capsule.Constrain(from, new float2(0.2f, 1.5f));
            Assert.Less(math.distance(new float2(-0.5f, 1.5f), slid), 1e-3f, "a slanted move slides along it");
            Assert.AreEqual(new float2(-0.6f, 0.5f), capsule.Constrain(from, new float2(-0.6f, 0.5f)),
                "backing away is free");
        }

        [Test]
        public void Capsule_PushedIntoADiagonalWall_SlidesAlongIt()
        {
            var wide = new NavAgent { Radius = 1f };
            _world.AddAgentPrefabs(wide);
            // A wall along x = z, stamped as a staircase of cells.
            _world.SpawnObstacle(float3.zero, new float2(1f, 40f), math.PI * 0.25f);
            _world.Tick();
            // Two cells wide, so it rides the step corners rather than sitting in the notches between them.
            var capsule = new NavCapsule(wide.Access(_world.Grid()), float2.zero);
            var along = math.normalize(new float2(1f, 1f));
            var move = 0.2f * new float2(math.cos(math.radians(20f)), math.sin(math.radians(20f)));

            var position = new float2(-4f, 4.3f);
            var start = math.dot(position, along);
            var deepest = 0f;
            for (var i = 0; i < 100; i++)
            {
                position = capsule.Constrain(position, position + move);
                deepest = math.max(deepest, capsule.Depth(position, float2.zero));
            }

            // Pushed 25 degrees into it, nearly all the move along it is kept; sliding on one axis keeps under 85 %.
            var ideal = 100f * math.dot(move, along);
            Assert.Greater(math.dot(position, along) - start, ideal * 0.9f, $"slid along it, ending at {position}");
            Assert.Less(position.x, position.y, "on its own side");
            Assert.Less(deepest, 0.05f, "and never sank into it");
        }
    }
}
