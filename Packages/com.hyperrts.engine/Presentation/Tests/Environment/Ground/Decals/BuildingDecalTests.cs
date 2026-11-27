using System.Linq;
using HyperRTS.Presentation.Environment.Ground.Decals;
using HyperRTS.Simulation.Common.Authoring;
using HyperRTS.Simulation.Common.Buildings;
using HyperRTS.Simulation.Vision;
using NUnit.Framework;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HyperRTS.Presentation.Tests.Environment.Ground.Decals;

/// <summary>Building decals: shown in the building's space only while it stands finished and seen, pooled after.</summary>
public class BuildingDecalTests
{
    private PresentationWorldKit _kit;
    private EntityManager _em;
    private BuildingDecalSystem _system;
    private Material _decal;

    [SetUp]
    public void SetUp()
    {
        _kit = new PresentationWorldKit("Building Decal Tests");
        _em = _kit.EntityManager;
        _system = _kit.World.CreateSystemManaged<BuildingDecalSystem>();
        _decal = new Material(Shader.Find("Shader Graphs/Decal"));
    }

    [TearDown]
    public void TearDown()
    {
        _kit.Dispose();
        Object.DestroyImmediate(_decal);
    }

    private static DecalProjector[] Shown() => Resources.FindObjectsOfTypeAll<DecalProjector>()
        .Where(p => p.transform.parent != null && p.transform.parent.name == "HyperRTS Building Decals" && p.enabled)
        .ToArray();

    private Entity Building(float3 position, quaternion rotation)
    {
        var building = _em.CreateEntity();
        _em.AddComponentData(building, new LocalToWorld { Value = float4x4.TRS(position, rotation, 1f) });
        _em.AddComponentData(building, new ConstructionProgress());
        BuildingDecalsAuthoring.Add(new EntityWriter(_em, building), new[]
        {
            new BuildingDecal { Material = _decal, Position = new float3(0f, 0.1f, 4f), Size = new float3(2f, 5f, 0.6f) },
        });
        return building;
    }

    [Test]
    public void Decals_ShowOnTheFinishedSeenBuilding_InItsSpace_AndGoWithIt()
    {
        var building = Building(new float3(10f, 0f, 5f), quaternion.RotateY(math.PI / 2f));
        _system.Update();
        Assert.IsEmpty(Shown(), "none under construction");

        _em.SetComponentEnabled<ConstructionProgress>(building, false);
        _system.Update();
        var shown = Shown();
        Assert.AreEqual(1, shown.Length);
        Assert.That(Vector3.Distance(new Vector3(14f, 0.1f, 5f), shown[0].transform.position), Is.LessThan(1e-4f),
            "the building's front turned a quarter to +x");
        Assert.That(Vector3.Distance(Vector3.down, shown[0].transform.forward), Is.LessThan(1e-4f), "projects down");
        Assert.That(Vector3.Distance(Vector3.right, shown[0].transform.up), Is.LessThan(1e-4f),
            "the texture's top along the building's front");
        Assert.AreEqual(new Vector3(2f, 5f, 0.6f), shown[0].size);
        var projector = shown[0];

        _em.AddComponent<FogHidden>(building);
        _system.Update();
        Assert.IsEmpty(Shown(), "hidden in fog");

        _em.RemoveComponent<FogHidden>(building);
        _system.Update();
        Assert.AreSame(projector, Shown().Single(), "the pooled projector is reused");

        _em.DestroyEntity(building);
        _system.Update();
        Assert.IsEmpty(Shown(), "gone with the building");
    }
}
