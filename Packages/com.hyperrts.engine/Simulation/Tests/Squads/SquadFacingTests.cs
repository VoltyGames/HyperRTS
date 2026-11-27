using HyperRTS.Simulation.Common.Geometry;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Simulation.Tests.Squads;

/// <summary>A squad given a faced move turns to the facing before its order finishes.</summary>
public class SquadFacingTests : SquadFixture
{
    [Test]
    public void Squad_GivenAFacedMove_EndsFacingIt()
    {
        var squad = World.SpawnArmedSquad(float3.zero, new byte[3]);

        World.Command(1, new PlayerCommand
        {
            Type = CommandType.Move, Unit = squad, Position = new float3(0f, 0f, 20f), Yaw = math.radians(90f),
            Faced = true,
        });
        World.Tick();

        Assert.IsTrue(World.IsEnabled<ActiveOrder>(squad), "the order starts");
        Assert.IsTrue(World.RunUntil(() => !World.IsEnabled<ActiveOrder>(squad), 30f), "the order finishes");
        var heading = math.normalize(World.Get<LocalTransform>(squad).Forward().xz);
        var off = math.acos(math.clamp(heading.x, -1f, 1f));
        Assert.LessOrEqual(off, Planar.FacingTolerance + 1e-3f, $"heading {heading}, {math.degrees(off)} degrees off");
    }
}
