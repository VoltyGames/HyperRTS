using HyperRTS.Presentation.Environment.Ground;
using HyperRTS.Presentation.Environment.Ground.Craters;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Tests.Environment.Ground;

/// <summary>The box and height a building's levelling is uploaded with.</summary>
public class BuildingLevelTests
{
    [Test]
    public void TurnedFootprint_ReachFollowsItsAxes()
    {
        var level = new BuildingLevel(new TurnedRect(float2.zero, new float2(0f, 1f), new float2(8f, 2f)), 0f);

        Assert.AreEqual(new float2(1f, 4f) + BuildingLevel.Skirt, level.Reach);
        Assert.IsTrue(level.Area.Contains(new float2(0.5f, 3.5f)), "its width runs north");
        Assert.IsFalse(level.Area.Contains(new float2(2f, 0f)));
    }

    [Test]
    public void Target_SitsBelowTheBase()
    {
        var level = new BuildingLevel(new TurnedRect(float2.zero, new float2(1f, 0f), new float2(2f, 2f)), 5f);

        Assert.AreEqual(5f - BuildingLevel.Drop, level.Target, 1e-6f);
    }

    [Test]
    public void Size_MatchesTheShaderStride() =>
        Assert.AreEqual(BuildingLevel.Stride, UnsafeUtility.SizeOf<BuildingLevel>());
}
