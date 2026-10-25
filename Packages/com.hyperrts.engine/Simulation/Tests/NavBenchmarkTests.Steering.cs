using System.Diagnostics;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    public partial class NavBenchmarkTests
    {
        private static readonly Locomotion TankHandling = new()
        {
            Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), Pivots = true,
            ReverseSpeed = 3f, ReverseDistance = 10f,
        };

        [Test]
        public void SteeringFrame_FiveHundredUnits()
        {
            var units = new Entity[500];
            for (var i = 0; i < 500; i++)
            {
                units[i] = SpawnCrowdMember(i);
            }

            _world.Tick();
            for (var i = 0; i < 500; i++)
            {
                var start = _world.GetPosition(units[i]);
                _world.EntityManager.SetComponentData(units[i],
                    new MoveDestination { Value = new float3(start.x, 0f, -start.z), Group = i < 250 ? 1 : 2 });
                _world.EntityManager.SetComponentEnabled<MoveDestination>(units[i], true);
            }

            // Until the heads of both crowds meet, so most units steer round someone.
            _world.Run(5f);
            double total = 0.0, slowest = 0.0;
            for (var frame = 0; frame < 60; frame++)
            {
                _world.Tick();
                var time = TimeSystem<MovementSystem>();
                total += time;
                slowest = math.max(slowest, time);
            }

            TestContext.WriteLine($"500-unit steering frame: mean {total / 60.0:F3} ms, max {slowest:F3} ms");
            Assert.Less(total / 60.0, 3.0, $"mean {total / 60.0:F3} ms, under twice 1.5 ms");
        }

        // Each crowd is 200 infantry beside 50 tanks in the clear west strip, the halves facing each other.
        private Entity SpawnCrowdMember(int i)
        {
            var north = i >= 250;
            var index = i % 250;
            var sign = north ? 1f : -1f;
            if (index < 200)
            {
                var soldier = new float3(-290f + index % 10 * 2f, 0f, sign * (45f + index / 10 * 2f));
                return _world.SpawnUnit(1, soldier, 5f, Infantry.Radius);
            }

            var slot = index - 200;
            var position = new float3(-268f + slot % 5 * 5f, 0f, sign * (45f + slot / 5 * 9f));
            var tank = _world.SpawnUnit(1, position, 7f, Tank.Radius);
            var agent = Tank;
            agent.HullLength = 7.5f;
            agent.Spacing = UnitAuthoring.DefaultSpacing;
            _world.EntityManager.SetComponentData(tank, agent);
            _world.EntityManager.SetComponentData(tank,
                LocalTransform.FromPositionRotation(position, quaternion.RotateY(north ? math.PI : 0f)));
            var writer = new EntityManagerWriter(_world.EntityManager, tank);
            LocomotionAuthoring.Add(ref writer, TankHandling);
            return tank;
        }
    }
}
