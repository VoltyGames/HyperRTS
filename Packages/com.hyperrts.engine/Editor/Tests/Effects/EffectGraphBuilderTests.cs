using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using HyperRTS.Editor.Effects.Graph;
using HyperRTS.Editor.Effects.Layer;
using HyperRTS.Presentation.Effects.Rig;
using NUnit.Framework;
using Unity.Scripting.LifecycleManagement;

namespace HyperRTS.Editor.Tests.Effects;

/// <summary>The test effects' graphs are current with their recipes and compile, and every kind's quads face a set way.</summary>
public class EffectGraphBuilderTests
{
    private const string TestBurst = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestBurst" + EffectRecipe.Extension;
    private const string TestStream = "Packages/com.hyperrts.engine/Presentation/Tests/Effects/TestStream" + EffectRecipe.Extension;

    // A new kind fails here until it is given a facing, so no quad ships in VFX Graph's fixed world axes.
    [NoAutoStaticsCleanup]
    private static readonly FrozenDictionary<EffectLayerKind, string> Facing = new Dictionary<EffectLayerKind, string>()
    {
        [EffectLayerKind.Glow] = "FaceCameraPlane",
        [EffectLayerKind.Puff] = "FaceCameraPlane",
        [EffectLayerKind.SixWay] = "FaceCameraPlane",
        [EffectLayerKind.Ring] = "FaceCameraPlane",
        [EffectLayerKind.Distort] = "FaceCameraPlane",
        [EffectLayerKind.Spark] = "AlongVelocity",
        [EffectLayerKind.Debris] = null,
        [EffectLayerKind.Source] = null,
    }.ToFrozenDictionary();

    [TestCase(TestBurst)]
    [TestCase(TestStream)]
    public void TestGraphs_AreCurrentAndCompile(string recipe)
    {
        Assert.IsNull(EffectGraphBuilder.Problem(recipe));
        CollectionAssert.IsEmpty(EffectGraphBuilder.ShaderErrors(EffectRecipe.GraphPath(recipe)));
    }

    [Test]
    public void EveryKind_HasItsFacing()
    {
        foreach (EffectLayerKind kind in Enum.GetValues<EffectLayerKind>())
        {
            Assert.IsTrue(Facing.TryGetValue(kind, out var mode), $"{kind} has no facing in this test.");
            Assert.AreEqual(mode, new EffectLayer { kind = kind }.OrientMode, kind.ToString());
        }
    }

    [Test]
    public void AnUprightQuad_TurnsAboutWorldUp()
    {
        Assert.AreEqual("FixedAxis", new EffectLayer { kind = EffectLayerKind.SixWay, orient = EffectLayerOrient.Upright }.OrientMode);
    }

    [Test]
    public void AFlatPuff_IsADecalOnTheGround()
    {
        var puff = new EffectLayer { kind = EffectLayerKind.Puff, orient = EffectLayerOrient.Flat };
        Assert.AreEqual("Advanced", puff.OrientMode);
        Assert.IsTrue(puff.Projected, "a flat puff is a decal");
        Assert.IsFalse(new EffectLayer { kind = EffectLayerKind.Distort, orient = EffectLayerOrient.Flat }.Projected, "a flat distortion stays a quad");
    }
}
