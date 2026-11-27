using HyperRTS.Presentation.Destruction;
using HyperRTS.Simulation.Death;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace HyperRTS.Presentation.Tests.Destruction;

/// <summary>Fresh remains have their parts placed once, the frame they appear.</summary>
public class RemainsPlacementTests
{
    private PresentationWorldKit _kit;
    private EntityManager _em;

    [SetUp]
    public void SetUp()
    {
        _kit = new PresentationWorldKit("Remains Placement Tests");
        _em = _kit.EntityManager;
    }

    [TearDown]
    public void TearDown() => _kit.Dispose();

    [Test]
    public void FreshRemains_HaveTheirPartsPlacedUnderTheRoot_Once()
    {
        var ruin = _em.CreateEntity(typeof(LocalToWorld), typeof(Unplaced));
        _em.AddComponentData(ruin, LocalTransform.FromPositionRotation(new float3(10f, 0f, 5f), quaternion.RotateY(math.PI * 0.5f)));
        var wall = Part(ruin, new float3(0f, 0f, 2f));
        var beam = Part(wall, new float3(0f, 3f, 0f));
        var group = _em.AddBuffer<LinkedEntityGroup>(ruin);
        group.Add(ruin);
        group.Add(beam);
        group.Add(wall);

        var system = _kit.World.CreateSystem<RemainsPlacementSystem>();
        system.Update(_kit.World.Unmanaged);

        // The root faces +X, so the wall 2 m ahead of it stands 2 m east.
        AssertAt(new float3(12f, 0f, 5f), wall);
        AssertAt(new float3(12f, 3f, 5f), beam);
        Assert.IsFalse(_em.IsComponentEnabled<Unplaced>(ruin));
    }

    // A part as a prefab instance has it: its own transform, and the place it was baked at.
    private Entity Part(Entity parent, float3 offset)
    {
        var part = _em.CreateEntity(typeof(LocalToWorld));
        _em.AddComponentData(part, LocalTransform.FromPosition(offset));
        _em.AddComponentData(part, new Parent { Value = parent });
        return part;
    }

    private void AssertAt(float3 expected, Entity part)
    {
        var position = _em.GetComponentData<LocalToWorld>(part).Position;
        Assert.Less(math.distance(expected, position), 1e-4f, $"{position}");
    }
}
