using System.Linq;
using HyperRTS.Editor.Templates;
using HyperRTS.Editor.Validation.Issues;
using HyperRTS.Editor.Validation.Rules.Units;
using HyperRTS.Editor.Validation.Sets;
using HyperRTS.Presentation.Rendering.LitTeam;
using HyperRTS.Simulation.Buildings;
using HyperRTS.Simulation.GameEntities;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HyperRTS.Editor.Tests.Validation.Rules.Units;

/// <summary>Team marker counts and team colour settings, past the broken case every rule has.</summary>
public class TeamColorRuleTests : TemplateFixture
{
    [Test]
    public void TeamColorRule_CountsMarkers_AndWarnsOnAnUnpaintedOne()
    {
        var unit = Template<UnitAuthoring>(TemplateObjects.Unit());
        Assert.IsEmpty(MarkerWarnings(unit), "the template carries the two markers it needs");

        RemoveMarker(unit);
        Assert.IsTrue(MarkerWarnings(unit).Any(message => message.Contains("Only 1 team markers")));

        AddMarker(unit, Track(new Material(Shader.Find("Universal Render Pipeline/Lit"))));
        Assert.IsTrue(MarkerWarnings(unit).Any(message => message.Contains("no _TeamColor material")));

        var prop = Template<UnitAuthoring>(TemplateObjects.Unit("Prop"));
        prop.owner = 0;
        RemoveMarker(prop);
        RemoveMarker(prop);
        Assert.IsEmpty(MarkerWarnings(prop), "a neutral entity nobody can capture or garrison never takes an owner");
    }

    [Test]
    public void TeamColorRule_AsksOneMarkerOfABuildingPlacedInLines()
    {
        var wall = Template<BuildingAuthoring>(TemplateObjects.Building("Wall"));
        Object.DestroyImmediate(wall.GetComponentInChildren<TeamMarkerAuthoring>().gameObject);
        Assert.IsTrue(MarkerWarnings(wall).Any(message => message.Contains("Only 1 team markers")));

        wall.placedInLines = true;
        Assert.IsEmpty(MarkerWarnings(wall), "its segments repeat along the line, and so would a second marker");
    }

    // A base prefab only exists to be derived from; its variant is placed, with the base's markers and its own.
    [Test]
    public void TeamColorRule_SkipsBasePrefabs_CountsInheritedMarkers()
    {
        const string folder = "Assets/_TeamMarkerTests";
        AssetDatabase.CreateFolder("Assets", "_TeamMarkerTests");
        try
        {
            var template = Track(TemplateObjects.Unit());
            RemoveMarker(template.GetComponent<UnitAuthoring>());
            var basePrefab = PrefabUtility.SaveAsPrefabAsset(template, folder + "/Base.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, folder + "/Variant.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
            var prefabs = new PrefabSet(new[] { basePrefab, variant });

            Assert.IsEmpty(MarkerWarnings(basePrefab.GetComponent<UnitAuthoring>(), prefabs));
            Assert.IsTrue(MarkerWarnings(variant.GetComponent<UnitAuthoring>(), prefabs)
                .Any(message => message.Contains("Only 1 team markers")));
        }
        finally
        {
            AssetDatabase.DeleteAsset(folder);
        }
    }

    [Test]
    public void TeamMarkerSettingsRule_ReportsNoAsset_AndTwoAssets_OnceEach()
    {
        Assert.AreEqual(1, TeamMarkerSettingsIssues().Count(issue => issue.Contains("No TeamMarkerSettings")));

        var first = Track(ScriptableObject.CreateInstance<TeamMarkerSettings>());
        Assert.IsEmpty(TeamMarkerSettingsIssues(first));

        var second = Track(ScriptableObject.CreateInstance<TeamMarkerSettings>());
        Assert.AreEqual(1, TeamMarkerSettingsIssues(first, second).Count(issue => issue.Contains("2 TeamMarkerSettings")));
    }

    private static string[] TeamMarkerSettingsIssues(params TeamMarkerSettings[] assets)
    {
        var issues = new ValidationIssues();
        new TeamMarkerSettingsRule().Check(AssetSet.From(assets), issues);
        return issues.Select(issue => issue.Message).ToArray();
    }

    private static void RemoveMarker(UnitAuthoring unit) =>
        Object.DestroyImmediate(unit.GetComponentInChildren<TeamMarkerAuthoring>().gameObject);

    private static void AddMarker(UnitAuthoring unit, Material material)
    {
        var marker = new GameObject("Marker", typeof(MeshFilter), typeof(MeshRenderer), typeof(TeamMarkerAuthoring));
        marker.transform.SetParent(unit.transform);
        marker.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    private static string[] MarkerWarnings(GameEntityAuthoring unit, PrefabSet prefabs = null)
    {
        var issues = new ValidationIssues();
        TeamColorRule.CheckMarkers(unit, 2, prefabs ?? new PrefabSet(new[] { unit.gameObject }), issues);
        return issues.Select(issue => issue.Message).ToArray();
    }
}
