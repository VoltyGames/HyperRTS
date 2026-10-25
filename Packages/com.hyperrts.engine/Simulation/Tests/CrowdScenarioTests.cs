using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>
    /// Whole-crowd steering: tanks through a gate and over a one-lane bridge, a column of long trucks along a road,
    /// infantry passing head-on, with no footprint ever sunk into a wall past the skin.
    /// </summary>
    public partial class CrowdScenarioTests
    {
        private const float WallSlack = NavTolerances.Skin + 0.05f;

        private TestWorld _world;
        private float _deepest;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, 500f);
            _deepest = 0f;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        private Entity SpawnVehicle(float3 position, float yaw, float speed, NavAgent agent, Locomotion locomotion)
        {
            var vehicle = _world.SpawnUnit(1, position, speed, agent.Radius);
            agent.Spacing = UnitAuthoring.DefaultSpacing;
            agent.WallMargin = agent.WallMarginFor(locomotion.TurnRadius);
            _world.EntityManager.SetComponentData(vehicle, agent);
            _world.EntityManager.SetComponentData(vehicle, LocalTransform.FromPositionRotation(position,
                quaternion.RotateY(yaw)));
            var writer = new EntityManagerWriter(_world.EntityManager, vehicle);
            LocomotionAuthoring.Add(ref writer, locomotion);
            return vehicle;
        }

        private void Send(Entity unit, float3 goal, int group)
        {
            _world.EntityManager.SetComponentData(unit, new MoveDestination { Value = goal, Group = group });
            _world.EntityManager.SetComponentEnabled<MoveDestination>(unit, true);
        }

        // Selects the units and gives one move, as a player's right-click does.
        private void OrderFormation(List<Entity> units, float3 goal)
        {
            foreach (var unit in units)
            {
                _world.EntityManager.SetComponentEnabled<Selected>(unit, true);
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.Move, Position = goal });
        }

        private bool AllArrived(List<Entity> units)
        {
            foreach (var unit in units)
            {
                if (_world.IsEnabled<MoveDestination>(unit))
                {
                    return false;
                }
            }

            return true;
        }

        // Runs until every unit has ended its move, tracking how deep any footprint point sank into a wall.
        private bool RunWatchingWalls(List<Entity> units, float cap)
        {
            for (var t = 0f; t < cap; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var grid = _world.Grid();
                foreach (var unit in units)
                {
                    _deepest = math.max(_deepest, WallDepth(grid, unit));
                }

                if (AllArrived(units))
                {
                    return true;
                }
            }

            return false;
        }

        private float WallDepth(in NavGrid grid, Entity unit)
        {
            var agent = _world.Get<NavAgent>(unit);
            var transform = _world.Get<LocalTransform>(unit);
            var axis = agent.SegmentAxis(transform.Rotation);
            var access = agent.Access(grid);
            var deepest = 0f;
            for (var i = 0; i <= 8; i++)
            {
                var point = transform.Position.xz + axis * (i / 4f - 1f);
                deepest = math.max(deepest, agent.Radius - access.Distance(point));
            }

            return deepest;
        }

        private string Stragglers(List<Entity> units)
        {
            var moving = new List<string>();
            foreach (var unit in units)
            {
                if (_world.IsEnabled<MoveDestination>(unit))
                {
                    var goal = _world.Get<MoveDestination>(unit).Value;
                    var speed = math.length(_world.Get<Velocity>(unit).Value);
                    var manoeuvre = _world.EntityManager.HasComponent<LocomotionState>(unit)
                        ? _world.Get<LocomotionState>(unit).IsManoeuvring
                        : false;
                    moving.Add($"{_world.GetPosition(unit).xz} to {goal.xz} at {speed:F1} m/s " +
                               $"facing {_world.Get<LocalTransform>(unit).Forward().xz} manoeuvring {manoeuvre}");
                }
            }

            return $"{moving.Count} still moving: {string.Join("; ", moving)}";
        }

        private void AssertNoneStalled(List<Entity> units)
        {
            foreach (var unit in units)
            {
                Assert.IsFalse(_world.Get<Arrival>(unit).Stalled, $"{unit} gave up at {_world.GetPosition(unit)}");
            }
        }

        // A formation order is what players give; single moves cover the corridor search instead of the group field.
        [TestCase(true)]
        [TestCase(false)]
        public void TwentyTanks_ThroughATwelveMetreGate_AllArrive(bool formation)
        {
            // A wall along x = 0 with a 12 m gate at z -6 to 6.
            _world.SpawnObstacle(new float3(0f, 0f, 46f), new float2(2f, 80f));
            _world.SpawnObstacle(new float3(0f, 0f, -46f), new float2(2f, 80f));
            var tank = new NavAgent { Radius = 1.85f, HullLength = 7.5f };
            var handling = new Locomotion
            {
                Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), Pivots = true,
                ReverseSpeed = 3f, ReverseDistance = 10f,
            };
            var units = new List<Entity>();
            for (var i = 0; i < 20; i++)
            {
                var start = new float3(-30f - i / 4 * 10f, 0f, -12f + i % 4 * 6f);
                units.Add(SpawnVehicle(start, math.PI * 0.5f, 7f, tank, handling));
            }

            _world.Tick();
            if (formation)
            {
                OrderFormation(units, new float3(55f, 0f, 0f));
            }
            else
            {
                for (var i = 0; i < 20; i++)
                {
                    Send(units[i], new float3(30f + i / 4 * 12f, 0f, -15f + i % 4 * 10f), 0);
                }
            }

            Assert.IsTrue(RunWatchingWalls(units, 120f), Stragglers(units));
            AssertNoneStalled(units);
            foreach (var unit in units)
            {
                var at = _world.GetPosition(unit);
                Assert.Greater(at.x, 10f, $"{unit} got through the gate, at {at}");
            }

            Assert.LessOrEqual(_deepest, WallSlack, $"no tank sank into the wall, deepest {_deepest:F3} m");
        }

        [Test]
        public void TruckColumn_FollowsARoadRoundACorner_AndThroughAGap()
        {
            // A road 20 m wide east along z = 0, north along x = 50 past a gap 10 m wide, into open ground.
            _world.SpawnObstacle(new float3(-80f, 0f, -16f), new float2(300f, 12f));
            _world.SpawnObstacle(new float3(-95f, 0f, 70f), new float2(270f, 120f));
            _world.SpawnObstacle(new float3(67f, 0f, 55f), new float2(14f, 150f));
            _world.SpawnObstacle(new float3(45f, 0f, 90f), new float2(10f, 4f));
            var truck = new NavAgent { Radius = 1.4f, HullLength = 17f };
            var handling = new Locomotion
            {
                Acceleration = 3f, Braking = 6f, TurnRate = math.radians(40f), TurnRadius = 12f,
                ReverseSpeed = 3f, ReverseDistance = 6f,
            };
            var units = new List<Entity>();
            for (var i = 0; i < 10; i++)
            {
                units.Add(SpawnVehicle(new float3(-20f - i * 22f, 0f, 0f), math.PI * 0.5f, 8f, truck, handling));
            }

            _world.Tick();
            for (var i = 0; i < 10; i++)
            {
                Send(units[i], new float3(20f + i % 5 * 15f, 0f, 170f + i / 5 * 25f), 1);
            }

            Assert.IsTrue(RunWatchingWalls(units, 180f), Stragglers(units));
            AssertNoneStalled(units);
            Assert.LessOrEqual(_deepest, WallSlack, $"no truck sank into a wall, deepest {_deepest:F3} m");
        }

        [Test]
        public void HundredInfantry_InTwoGroups_PassHeadOn()
        {
            var units = new List<Entity>();
            for (var i = 0; i < 100; i++)
            {
                var west = i < 50;
                var row = new float2(i % 50 / 10 * 1.6f, i % 10 * 1.6f - 7.2f);
                var start = new float3(west ? -30f - row.x : 30f + row.x, 0f, row.y);
                units.Add(_world.SpawnUnit(1, start, 5f, 0.5f));
            }

            _world.Tick();
            for (var i = 0; i < 100; i++)
            {
                var start = _world.GetPosition(units[i]);
                Send(units[i], new float3(-start.x, 0f, start.z), i < 50 ? 1 : 2);
            }

            Assert.IsTrue(RunWatchingWalls(units, 60f), Stragglers(units));
            for (var i = 0; i < 100; i++)
            {
                var x = _world.GetPosition(units[i]).x;
                Assert.Greater(i < 50 ? x : -x, 15f, $"soldier {i} got past the other group");
            }
        }
    }
}
