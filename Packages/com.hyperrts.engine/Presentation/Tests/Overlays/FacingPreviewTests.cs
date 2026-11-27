using HyperRTS.Presentation.Interaction;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Commands;
using HyperRTS.Simulation.Navigation.Agents;
using HyperRTS.Simulation.Navigation.Grid;
using HyperRTS.Simulation.Orders;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Overlays;

/// <summary>The facing drag's threshold, its direction and yaw from two ground points, and the preview's slots matching the order's.</summary>
public class FacingPreviewTests
{
    [TestCase(0f, 5f, 0f, 0f)]
    [TestCase(5f, 0f, 1f, 90f)]
    [TestCase(-5f, 0f, -1f, -90f)]
    [TestCase(0f, -5f, 0f, 180f)]
    public void DragReadsAsFacingAndYaw(float dragX, float dragZ, float facingX, float degrees)
    {
        var drag = new FacingDragState { Press = new float3(2f, 1f, 3f), Pointer = new float3(2f + dragX, 4f, 3f + dragZ) };

        Assert.AreEqual(facingX, drag.Facing.x, 1e-5f);
        Assert.AreEqual(1f, math.length(drag.Facing), 1e-5f);
        Assert.AreEqual(math.radians(degrees), drag.Yaw, 1e-5f);
    }

    [TestCase(FacingDragState.ThresholdPixels - 1f, false)]
    [TestCase(FacingDragState.ThresholdPixels + 1f, true)]
    public void ReleasePastTheThresholdIssuesAFacedMove(float pixels, bool faced)
    {
        var drag = FacingDragState.Create(CommandType.Move, 0, false, new float2(100f, 100f), new float3(0f, 0f, 20f));
        drag.Pointer = new float3(8f, 0f, 20f);

        drag.Aim(new float2(100f + pixels, 100f));
        var command = drag.Command();

        Assert.AreEqual(faced, command.Faced);
        Assert.AreEqual(new float3(0f, 0f, 20f), command.Position, "the units go to the press point");
        Assert.AreEqual(faced ? math.radians(90f) : 0f, command.Yaw, 1e-5f);
    }

    [Test]
    public void SlotsMatchTheFormationTheOrderLays()
    {
        var agent = new NavAgent { Radius = 1f, Layer = NavLayer.Air };
        var members = new NativeArray<FormationMember>(5, Allocator.Temp);
        var positions = new NativeArray<float3>(5, Allocator.Temp);
        var spacings = new NativeArray<float2>(5, Allocator.Temp);
        for (var i = 0; i < members.Length; i++)
        {
            positions[i] = new float3(i * 3f, 0f, -10f);
            members[i] = new FormationMember(positions[i], agent, 0f);
            spacings[i] = agent.SlotSpacing;
        }

        var drag = new FacingDragState { Aimed = true, Press = new float3(0f, 0f, 20f), Pointer = new float3(8f, 0f, 20f) };
        var map = new MapSettings { Min = new float2(-100f), Size = new float2(200f) };
        using var expected = new NativeArray<float3>(5, Allocator.Temp);
        using var actual = new NativeArray<float3>(5, Allocator.Temp);

        Formation.Assign(positions, drag.Press, drag.Facing, spacings, expected);
        new FormationSlots(members).Assign(drag.Press, drag.Facing, map, default, actual);

        for (var i = 0; i < members.Length; i++)
        {
            Assert.That(actual[i], Is.EqualTo(expected[i]).Using(new Float3Comparer()), $"slot {i}");
        }

        members.Dispose();
        positions.Dispose();
        spacings.Dispose();
    }

    private class Float3Comparer : System.Collections.Generic.IEqualityComparer<float3>
    {
        public bool Equals(float3 a, float3 b) => math.distance(a, b) < 1e-4f;

        public int GetHashCode(float3 value) => 0;
    }
}
