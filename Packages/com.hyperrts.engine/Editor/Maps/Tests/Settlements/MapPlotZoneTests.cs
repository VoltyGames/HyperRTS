using System.Linq;
using HyperRTS.Editor.Maps.Land;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Placement;
using NUnit.Framework;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Tests.Settlements;

/// <summary>How deep a village plot's yard and garden run, and what stands in the garden.</summary>
public class MapPlotZoneTests
{
    // The fixture's plot is 24 by 36 m behind a 5 m front yard, its house 8 m deep and its garden half the rest.
    private const float Width = 24f;
    private const float Depth = 36f;
    private const float HouseBack = 5f + 8f;
    private const float GardenEnd = HouseBack + 0.5f * (Depth - HouseBack);

    // Road half width plus the front gap: where each plot's front fence stands.
    private const float FrontFromRoad = 3f + 2f;

    [Test]
    public void TheGardenTakesItsShare_WhenNoOutbuildingFitsTheYard()
    {
        using var fixture = MapSettlementFixture.Plan(spawn: new Vector2(100f, -100f), edit: kit =>
        {
            kit.outbuildings[0] = "WideShed";
            kit.extras.Add(new MapPlotProp { kind = "Well", chance = 1f, zone = MapPlotZone.Garden });
        });

        var land = fixture.Context.Land;
        var houses = fixture.InSector("House").Count;
        var garden = 0;
        land.ForEach((_, _, center) => garden += center.y <= 0f && land.KindAt(center) == MapLandKind.Garden ? 1 : 0);
        var perPlot = garden * MapLand.CellSize * MapLand.CellSize / houses;
        var expected = (GardenEnd - HouseBack) * Width;
        Assert.AreEqual(6, houses);
        Assert.AreEqual(expected, perPlot, expected * 0.12f, "the room kept for the shed goes to the garden");

        var gardenWells = fixture.InSector("Well").Select(BackFromFront).Where(v => v > HouseBack).ToList();
        Assert.AreEqual(houses, gardenWells.Count, "a garden extra with chance 1 stands in every garden");
        Assert.IsTrue(gardenWells.All(v => v < GardenEnd), "inside the garden, not the back");
    }

    private static float BackFromFront(MapPlacement placement) =>
        Mathf.Abs(placement.Position.y - MapSettlementFixture.RoadZ) - FrontFromRoad;
}
