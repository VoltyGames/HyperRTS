using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Spatial;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Tanks over a one-lane bridge, alone and two in a column, never touching.</summary>
    public class CrowdBridgeTests : CrowdScenarioFixture
    {
        [TestCase(1, 1, 40f, true)]
        [TestCase(2, 1, 40f, false)]
        public void Tanks_CrossAOneLaneBridge_WithoutDrivingIntoEachOther(int count, int abreast, float goalX,
            bool formation)
        {
            World.SpawnBridge();
            var units = SpawnColumn(count, abreast);
            World.Tick();
            OrderColumn(units, abreast, goalX, formation);

            var worst = new Overlap();
            for (var t = 0f; t < 150f; t += TestWorld.FrameTime)
            {
                World.Tick();
                worst.Record(DeepestHullOverlap(units, out var at), t, at);
                if (AllArrived(units))
                {
                    break;
                }
            }

            for (var t = 0f; t < 5f; t += TestWorld.FrameTime)
            {
                World.Tick();
                worst.Record(DeepestHullOverlap(units, out var at), -t, at);
            }

            var behind = DescribeUnitsBehind(units);
            Assert.IsEmpty(behind,
                $"deepest {worst.Depth:F2} m at {worst.When:F1} s at {worst.Where}; not across: {string.Join("; ", behind)}");
            Assert.LessOrEqual(worst.Depth, 0f,
                $"deepest {worst.Depth:F2} m at {worst.When:F1} s at {worst.Where}; " + Stragglers(units));
        }

        // Rolling north past the mouth, far off its leg east, a tank must stop and pivot in rather than loop round.
        [TestCase(1.3f, 0.18f, -9f)]
        [TestCase(-0.1f, 0.24f, -3f)]
        [TestCase(-1.3f, 2.96f, 9f)]
        public void Tank_RollingPastTheBridgeMouth_PivotsIntoIt(float z, float yaw, float goalZ)
        {
            World.SpawnBridge();
            var tank = TestVehicles.TankHull();
            var unit = World.SpawnVehicle(1, TestVehicles.At(new float3(-8.6f, 0f, z), yaw), 4.5f, tank,
                TestVehicles.Tracks);
            var facing = new float2(math.sin(yaw), math.cos(yaw));
            var rolling = new LocomotionState { Speed = 1.8f, LastChoice = facing * 1.8f };
            World.EntityManager.SetComponentData(unit, rolling);
            World.Send(unit, new float3(47f, 0f, goalZ), 0);
            var widest = 0f;
            for (var t = 0f; t < 40f && World.IsEnabled<MoveDestination>(unit); t += TestWorld.FrameTime)
            {
                World.Tick();
                var at = World.GetPosition(unit);
                widest = at.x < -6f ? math.max(widest, math.abs(at.z)) : widest;
            }

            Assert.Greater(World.GetPosition(unit).x, 30f, $"crossed, at {World.GetPosition(unit)}");
            Assert.Less(widest, 3f, $"stayed by the mouth, widest {widest:F2} m off the lane");
        }

        private struct Overlap
        {
            public float Depth;
            public float When;
            public float3 Where;

            public void Record(float depth, float when, float3 where)
            {
                if (depth > Depth)
                {
                    Depth = depth;
                    When = when;
                    Where = where;
                }
            }
        }

        private List<Entity> SpawnColumn(int count, int abreast)
        {
            var tank = TestVehicles.TankHull();
            var units = new List<Entity>();
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-25f - i / abreast * 11f, 0f, (i % abreast - (abreast - 1) * 0.5f) * 5f);
                units.Add(World.SpawnVehicle(1, TestVehicles.At(start, math.PI * 0.5f), 4.5f, tank,
                    TestVehicles.Tracks));
            }

            return units;
        }

        private void OrderColumn(List<Entity> units, int abreast, float goalX, bool formation)
        {
            if (formation)
            {
                OrderFormation(units, new float3(goalX, 0f, 0f));
                return;
            }

            for (var i = 0; i < units.Count; i++)
            {
                var lane = (i % abreast - (abreast - 1) * 0.5f) * 6f;
                World.Send(units[i], new float3(goalX + i / abreast * 11f, 0f, lane), 0);
            }
        }

        private List<string> DescribeUnitsBehind(List<Entity> units)
        {
            var behind = new List<string>();
            foreach (var unit in units)
            {
                var at = World.GetPosition(unit);
                if (at.x <= 8f)
                {
                    var speed = math.length(World.Get<Velocity>(unit).Value);
                    behind.Add($"{at.xz} facing {World.Get<LocalTransform>(unit).Forward().xz} at {speed:F1} m/s " +
                               $"to {World.Get<MoveDestination>(unit).Value.xz} " +
                               $"moving {World.IsEnabled<MoveDestination>(unit)} " +
                               $"ended {World.Get<Arrival>(unit).Reason}");
                }
            }

            return behind;
        }

        private float DeepestHullOverlap(List<Entity> units, out float3 at)
        {
            var deepest = 0f;
            at = float3.zero;
            for (var i = 0; i < units.Count; i++)
            {
                for (var j = i + 1; j < units.Count; j++)
                {
                    var a = Hull(units[i]);
                    var gap = math.length(Hull(units[j]).ClosestOffset(a.Position.xz, a.SegmentAxis));
                    if (a.Radius * 2f - gap > deepest)
                    {
                        deepest = a.Radius * 2f - gap;
                        at = a.Position;
                    }
                }
            }

            return deepest;
        }

        private SpatialEntry Hull(Entity unit)
        {
            var transform = World.Get<LocalTransform>(unit);
            var agent = World.Get<NavAgent>(unit);
            return new SpatialEntry
            {
                Position = transform.Position,
                Radius = agent.Radius,
                SegmentAxis = agent.SegmentAxis(transform.Rotation),
            };
        }
    }
}
