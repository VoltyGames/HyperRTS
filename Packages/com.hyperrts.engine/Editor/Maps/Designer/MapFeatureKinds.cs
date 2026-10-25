using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Spec;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using static HyperRTS.Editor.Maps.Designer.MapFeatureKind;

namespace HyperRTS.Editor.Maps.Designer
{
    /// <summary>Every feature list of a map spec, in drawing order (areas under lines under points).</summary>
    public static class MapFeatureKinds
    {
        [NoAutoStaticsCleanup]
        public static readonly IReadOnlyList<MapFeatureKind> All = new[]
        {
            Polygon(nameof(MapSpec.farmland), "Farmland", new Color(0.85f, 0.75f, 0.3f), nameof(MapFarmlandSpec.points)),
            Polygon(nameof(MapSpec.areas), "Areas", new Color(0.55f, 0.8f, 0.4f), nameof(MapAreaSpec.points)),
            Polygon(nameof(MapSpec.woods), "Woods", new Color(0.15f, 0.55f, 0.2f), nameof(MapWoodSpec.points)),
            Polygon(nameof(MapSpec.scatters), "Scatters", new Color(0.9f, 0.6f, 0.6f), nameof(MapScatterSpec.points)),
            Circle(nameof(MapSpec.hills), "Hills", new Color(0.75f, 0.6f, 0.35f),
                nameof(MapHillSpec.pos), nameof(MapHillSpec.radius)),
            Circle(nameof(MapSpec.settlements), "Settlements", new Color(0.95f, 0.45f, 0.3f),
                nameof(MapSettlementSpec.center), nameof(MapSettlementSpec.radius)),
            Polyline(nameof(MapSpec.rivers), "Rivers", new Color(0.25f, 0.5f, 0.95f), nameof(MapRiverSpec.points)),
            Polyline(nameof(MapSpec.ravines), "Ravines", new Color(0.6f, 0.4f, 0.25f), nameof(MapRavineSpec.points)),
            Polyline(nameof(MapSpec.belts), "Belts", new Color(0.35f, 0.7f, 0.3f), nameof(MapBeltSpec.points)),
            Polyline(nameof(MapSpec.lines), "Lines", new Color(0.8f, 0.5f, 0.9f), nameof(MapLineSpec.points)),
            Polyline(nameof(MapSpec.roads), "Roads", new Color(0.92f, 0.9f, 0.85f), nameof(MapRoadSpec.points)),
            Point(nameof(MapSpec.props), "Props", new Color(0.85f, 0.8f, 0.7f), nameof(MapPropSpec.pos)),
            Point(nameof(MapSpec.resources), "Resources", new Color(1f, 0.85f, 0.2f), nameof(MapResourceSpec.pos)),
            Point(nameof(MapSpec.spawns), "Spawns", new Color(0.2f, 0.6f, 1f), nameof(MapSpawnSpec.pos)),
        };
        public static MapFeatureKind Find(string field)
        {
            foreach (var kind in All)
            {
                if (kind.Field == field)
                {
                    return kind;
                }
            }

            throw new ArgumentException($"No map feature kind for spec list '{field}'.");
        }
    }
}
