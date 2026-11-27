using System.Collections.Generic;
using HyperRTS.Simulation.Movement;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Movement;

/// <summary>Shared setup and helpers for the CrowdScenarioTests family.</summary>
public abstract class CrowdScenarioFixture : SimulationFixture
{
    protected const float WallSlack = NavTolerances.WallOverlap + 0.05f;

    protected float _deepest;

    protected override float MapSize => 500f;

    [SetUp]
    public void ResetDepth() => _deepest = 0f;

    // Selects the units and gives one move, as a player's right-click does.
    protected void OrderFormation(List<Entity> units, float3 goal)
    {
        foreach (var unit in units)
        {
            World.SetEnabled<Selected>(unit, true);
        }

        World.Command(1, new PlayerCommand { Type = CommandType.Move, Position = goal });
    }

    // Every ordered move has ended; a parked unit making way for the last of them is not on an order.
    protected bool AllArrived(List<Entity> units)
    {
        foreach (var unit in units)
        {
            if (World.IsEnabled<MoveDestination>(unit) && !World.Get<MoveDestination>(unit).GivingWay)
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
            World.Tick();
            var grid = World.Grid();
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
        var agent = World.Get<NavAgent>(unit);
        var transform = World.Get<LocalTransform>(unit);
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
            if (World.IsEnabled<MoveDestination>(unit))
            {
                var goal = World.Get<MoveDestination>(unit).Value;
                var speed = math.length(World.Get<Velocity>(unit).Value);
                var manoeuvre = World.EntityManager.HasComponent<LocomotionState>(unit)
                    ? World.Get<LocomotionState>(unit).IsManoeuvring
                    : false;
                moving.Add($"{World.GetPosition(unit).xz} to {goal.xz} at {speed:F1} m/s " +
                           $"facing {World.Get<LocalTransform>(unit).Forward().xz} manoeuvring {manoeuvre}");
            }
        }

        return $"{moving.Count} still moving: {string.Join("; ", moving)}";
    }

    // A unit may give up only beside its goal, where a groupmate stands on it: nothing is shoved off a slot.
    protected void AssertNoneStalledShort(List<Entity> units, float near)
    {
        foreach (var unit in units)
        {
            if (World.Get<Arrival>(unit).Reason != ArrivalReason.Stalled)
            {
                continue;
            }

            var goal = World.Get<MoveDestination>(unit).Value;
            var at = World.GetPosition(unit);
            Assert.Less(math.distance(at.xz, goal.xz), near, $"{unit} gave up at {at}, short of {goal}");
        }
    }
}
