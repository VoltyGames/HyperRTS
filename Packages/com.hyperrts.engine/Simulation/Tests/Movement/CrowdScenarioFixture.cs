using System.Collections.Generic;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Drive;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using HyperRTS.Simulation.Movement;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Movement
{
    /// <summary>Shared setup and helpers for the CrowdScenarioTests family.</summary>
    public abstract class CrowdScenarioFixture
    {
        protected const float WallSlack = NavTolerances.WallOverlap + 0.05f;

        protected TestWorld _world;
        protected float _deepest;

        [SetUp]
        public void SetUp()
        {
            _world = new TestWorld();
            _world.CreateMatch(new byte[] { 1, 2 }, 500f);
            _deepest = 0f;
        }

        [TearDown]
        public void TearDown() => _world.Dispose();

        // Selects the units and gives one move, as a player's right-click does.
        protected void OrderFormation(List<Entity> units, float3 goal)
        {
            foreach (var unit in units)
            {
                _world.SetEnabled<Selected>(unit, true);
            }

            _world.Command(1, new PlayerCommand { Type = CommandType.Move, Position = goal });
        }

        protected bool AllArrived(List<Entity> units)
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
        protected bool RunWatchingWalls(List<Entity> units, float cap)
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
                    UnityEngine.Debug.Log($"[{TestContext.CurrentContext.Test.Name}] all arrived in {t:F1} s");
                    return true;
                }
            }

            return false;
        }

        protected float WallDepth(in NavGrid grid, Entity unit)
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

        protected string Stragglers(List<Entity> units)
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

        protected void AssertNoneStalled(List<Entity> units)
        {
            foreach (var unit in units)
            {
                Assert.AreNotEqual(ArrivalReason.Stalled, _world.Get<Arrival>(unit).Reason,
                    $"{unit} gave up at {_world.GetPosition(unit)}");
            }
        }
    }
}
