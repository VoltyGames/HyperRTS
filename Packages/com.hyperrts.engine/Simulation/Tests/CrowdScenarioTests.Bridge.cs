using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Navigation;
using HyperRTS.Simulation.Units;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>Tanks over a one-lane bridge, alone, in columns and formations, never touching.</summary>
    public partial class CrowdScenarioTests
    {
        // Abreast tanks can't both fit on the deck; the push between them points into the rails, so one must yield.
        [TestCase(1, 1, 40f, false)]
        [TestCase(1, 1, 40f, true)]
        [TestCase(2, 1, 40f, false)]
        [TestCase(8, 2, 40f, true)]
        [TestCase(16, 4, 40f, true)]
        [TestCase(16, 4, 40f, false)]
        [TestCase(16, 4, 30f, true)]
        [TestCase(16, 4, 14f, false)]
        public void Tanks_CrossAOneLaneBridge_WithoutDrivingIntoEachOther(int count, int abreast, float goalX,
            bool formation)
        {
            _world.SpawnNavArea(float3.zero, new float2(12f, 600f), NavAreaKind.Water);
            _world.SpawnNavArea(float3.zero, new float2(20f, 6f), NavAreaKind.Deck);
            var tank = new NavAgent { Radius = 1.85f, HullLength = 7.9f };
            var handling = new Locomotion
            {
                Acceleration = 2.5f, Braking = 6f, TurnRate = math.radians(50f), Pivots = true,
                ReverseSpeed = 3f, ReverseDistance = 10f,
            };
            var units = new List<Entity>();
            for (var i = 0; i < count; i++)
            {
                var start = new float3(-25f - i / abreast * 11f, 0f, (i % abreast - (abreast - 1) * 0.5f) * 5f);
                units.Add(SpawnVehicle(start, math.PI * 0.5f, 4.5f, tank, handling));
            }

            _world.Tick();
            if (formation)
            {
                OrderFormation(units, new float3(goalX, 0f, 0f));
            }
            else
            {
                for (var i = 0; i < count; i++)
                {
                    var lane = (i % abreast - (abreast - 1) * 0.5f) * 6f;
                    Send(units[i], new float3(goalX + i / abreast * 11f, 0f, lane), 0);
                }
            }

            var deepest = 0f;
            var when = 0f;
            var where = float3.zero;
            for (var t = 0f; t < 150f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var depth = DeepestHullOverlap(units, out var at);
                if (depth > deepest)
                {
                    deepest = depth;
                    when = t;
                    where = at;
                }

                if (AllArrived(units))
                {
                    break;
                }
            }

            for (var t = 0f; t < 5f; t += TestWorld.FrameTime)
            {
                _world.Tick();
                var depth = DeepestHullOverlap(units, out var at);
                if (depth > deepest)
                {
                    deepest = depth;
                    when = -t;
                    where = at;
                }
            }

            var behind = new List<string>();
            foreach (var unit in units)
            {
                var at = _world.GetPosition(unit);
                if (at.x <= 8f)
                {
                    var speed = math.length(_world.Get<Velocity>(unit).Value);
                    behind.Add($"{at.xz} facing {_world.Get<LocalTransform>(unit).Forward().xz} at {speed:F1} m/s " +
                               $"to {_world.Get<MoveDestination>(unit).Value.xz} " +
                               $"moving {_world.IsEnabled<MoveDestination>(unit)} " +
                               $"stalled {_world.Get<Arrival>(unit).Stalled}");
                }
            }

            Assert.IsEmpty(behind,
                $"deepest {deepest:F2} m at {when:F1} s at {where}; not across: {string.Join("; ", behind)}");
            Assert.LessOrEqual(deepest, 0f, $"deepest {deepest:F2} m at {when:F1} s at {where}; " + Stragglers(units));
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
            var transform = _world.Get<LocalTransform>(unit);
            var agent = _world.Get<NavAgent>(unit);
            return new SpatialEntry
            {
                Position = transform.Position,
                Radius = agent.Radius,
                SegmentAxis = agent.SegmentAxis(transform.Rotation),
            };
        }
    }
}
