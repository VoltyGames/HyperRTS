using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>
    /// Whole-crowd steering: tanks through a gate and over a one-lane bridge, a column of long trucks along a road,
    /// infantry passing head-on, with no footprint ever sunk into a wall past the skin.
    /// </summary>
    public class CrowdScenarioTests : CrowdScenarioFixture
    {
        [Test]
        public void TwentyTanks_ThroughATwelveMetreGate_AllArrive()
        {
            // A wall along x = 0 with a 12 m gate at z -6 to 6.
            _world.SpawnObstacle(new float3(0f, 0f, 46f), new float2(2f, 80f));
            _world.SpawnObstacle(new float3(0f, 0f, -46f), new float2(2f, 80f));
            var tank = TestVehicles.TankHull();
            tank.HullLength = 7.5f;
            tank.WallMargin = tank.HullWallMargin;
            var units = new List<Entity>();
            for (var i = 0; i < 20; i++)
            {
                var start = new float3(-30f - i / 4 * 10f, 0f, -12f + i % 4 * 6f);
                units.Add(_world.SpawnVehicle(1, start, math.PI * 0.5f, 7f, tank, TestVehicles.Tracks));
            }

            _world.Tick();
            OrderFormation(units, new float3(55f, 0f, 0f));

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
            var truck = TestVehicles.Hull(new NavAgent
            {
                Radius = 1.4f, HullLength = 17f, Mass = 17f,
                Chassis = new Chassis { Wheelbase = 6f, RearAxle = 2.5f, TurnRadius = 12f, ReverseRun = 6f },
            });
            var units = new List<Entity>();
            for (var i = 0; i < 10; i++)
            {
                units.Add(_world.SpawnVehicle(1, new float3(-20f - i * 22f, 0f, 0f), math.PI * 0.5f, 8f, truck, TestVehicles.Wheels));
            }

            _world.Tick();
            for (var i = 0; i < 10; i++)
            {
                _world.Send(units[i], new float3(20f + i % 5 * 15f, 0f, 170f + i / 5 * 25f), 1);
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
                _world.Send(units[i], new float3(-start.x, 0f, start.z), i < 50 ? 1 : 2);
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
