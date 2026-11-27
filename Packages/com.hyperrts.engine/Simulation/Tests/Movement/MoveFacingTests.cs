using System.Linq;
using HyperRTS.Simulation.Orders;
using HyperRTS.Simulation.Selection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Tests.Movement;

/// <summary>Moves that name a facing: the unit turns to it before the order finishes, each queued leg keeps its own, and aircraft that can't hover ignore it.</summary>
public class MoveFacingTests : LocomotionFixture
{
    private static readonly float2 East = new(1f, 0f);
    private static readonly float2 South = new(0f, -1f);

    private void Move(Entity unit, float3 goal, float yaw, bool faced = true, bool queue = false) =>
        World.Command(1, new PlayerCommand
        {
            Type = CommandType.Move, Unit = unit, Position = goal, Yaw = yaw, Faced = faced, Queue = queue,
        });

    // The queue hands over a frame after a leg finishes, so a unit between legs is not yet done.
    private bool Finished(Entity unit) =>
        !World.IsEnabled<ActiveOrder>(unit) && World.Buffer<QueuedOrder>(unit).Length == 0;

    private void AssertFacing(Entity unit, float2 expected)
    {
        var off = TurnBetween(Facing(unit), expected);
        Assert.LessOrEqual(off, MoveOrderSystem.FacingTolerance + 1e-3f, $"{math.degrees(off)} degrees off");
    }

    [Test]
    public void Tank_GivenAFacedMove_EndsFacingItAndFinishes()
    {
        var tank = SpawnTank(float3.zero);

        Move(tank, new float3(0f, 0f, 20f), math.radians(90f));
        World.Tick();
        Assert.AreEqual(East.x, World.Get<ActiveOrder>(tank).Value.Facing.x, 1e-4f);

        Assert.IsTrue(World.RunUntil(() => Finished(tank), 30f), "the order finishes");
        AssertFacing(tank, East);
    }

    [Test]
    public void TankGroup_GivenAFacedMove_EachEndsFacingItAndFinishes()
    {
        var tanks = new[] { -6.6f, -2.2f, 2.2f, 6.6f }.Select(x => SpawnTank(new float3(x, 0f, 0f))).ToArray();
        foreach (var tank in tanks)
        {
            World.SetEnabled<Selected>(tank, true);
        }

        Move(Entity.Null, new float3(0f, 0f, 30f), math.radians(90f));
        World.Tick();

        Assert.IsTrue(World.RunUntil(() => tanks.All(Finished), 60f), "every order finishes");
        var off = tanks.Select(tank => math.degrees(TurnBetween(Facing(tank), East))).ToArray();
        Assert.IsTrue(off.All(degrees => degrees <= math.degrees(MoveOrderSystem.FacingTolerance) + 0.1f),
            $"degrees off: {string.Join(", ", off)}");
    }

    [Test]
    public void QueuedLegs_EachKeepTheirOwnFacing()
    {
        var tank = SpawnTank(float3.zero);

        Move(tank, new float3(0f, 0f, 20f), math.radians(90f));
        Move(tank, new float3(20f, 0f, 20f), math.radians(180f), queue: true);
        World.Tick();
        Assert.AreEqual(South.y, World.Buffer<QueuedOrder>(tank)[0].Value.Facing.y, 1e-4f, "the second leg's own facing");

        Assert.IsTrue(World.RunUntil(() => Finished(tank), 60f), "both legs finish");
        AssertFacing(tank, South);
    }

    [Test]
    public void PlainMove_HasNoFacing_AndFinishesOnArrival()
    {
        var tank = SpawnTank(float3.zero);

        Move(tank, new float3(0f, 0f, 20f), math.radians(90f), faced: false);
        World.Tick();
        Assert.AreEqual(float2.zero, World.Get<ActiveOrder>(tank).Value.Facing);

        Assert.IsTrue(World.RunUntil(() => Finished(tank), 30f), "the order finishes");
        AssertFacing(tank, new float2(0f, 1f));
    }

    [Test]
    public void FixedWingAircraft_IgnoresTheFacing()
    {
        var jet = World.SpawnAircraft(1, float3.zero, loiterRadius: 100f);

        Move(jet, new float3(0f, 0f, 50f), math.radians(90f));
        World.Tick();

        Assert.AreEqual(float2.zero, World.Get<ActiveOrder>(jet).Value.Facing);
    }
}
