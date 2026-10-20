using System;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Capsule footprints and spacing: long hulls keep their gap side by side and nose to tail.</summary>
    public class HullSpacingTests
    {
        private const float TankRadius = 1.85f;
        private const float TankLength = 6.9f;
        private const float Spacing = 0.6f;

        private TestWorld _world;

        [SetUp]
        public void SetUp() => _world = new TestWorld();

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

        private bool Arrived(Entity tank) => !_world.IsEnabled<ActiveOrder>(tank);

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
            _world.CreateMatch(1, 2);
            var tanks = new Entity[4];
            for (var i = 0; i < tanks.Length; i++)
            {
                tanks[i] = SpawnTank(new float3(-30f, 0f, i * 8f - 12f), new float3(1f, 0f, 0f));
                _world.EntityManager.SetComponentEnabled<Selected>(tanks[i], true);
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.Smart, Position = new float3(20f, 0f, 0f) });
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
    }
}
