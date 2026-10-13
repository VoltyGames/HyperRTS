using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Checks a spec before it is refined: its size, terrain and amounts are sane and every kind it names is in the
    /// climate, the palette or the rules. Where features lie (sector, bounds, symmetry) is the expander's to check.
    /// </summary>
    public sealed class MapSpecCheck : MapKeyCheck
    {
        private const float BridgeOverhang = 4f;
        private readonly MapSpec _spec;
        private readonly MapRules _rules;

        private MapSpecCheck(MapSpec spec, MapClimate climate, MapPalette palette, MapRules rules, MapIssues issues)
            : base(climate, palette, issues)
        {
            _spec = spec;
            _rules = rules;
        }

        public static void Check(MapSpec spec, MapClimate climate, MapPalette palette, MapRules rules, MapIssues issues) =>
            new MapSpecCheck(spec, climate, palette, rules, issues).Run();

        private void Run()
        {
            Header();
            Require(MapKeys.Contains(_rules.modes, _spec.mode), $"The map rules have no thresholds for mode '{_spec.mode}'.");
            Each(_spec.resources, nameof(MapSpec.resources), "Resource", (resource, name) =>
            {
                Entry(resource.kind, name);
                Require(resource.amount > 0, $"{name} ('{resource.kind}') needs an amount above 0.");
            });
            Each(_spec.props, nameof(MapSpec.props), "Prop", (prop, name) =>
            {
                Entry(prop.kind, name);
                Unaligned(prop.kind, name);
            });
            Each(_spec.scatters, nameof(MapSpec.scatters), "Scatter", (scatter, name) => Entry(scatter.kind, name));
            Each(_spec.lines, nameof(MapSpec.lines), "Line", Line);
            Each(_spec.roads, nameof(MapSpec.roads), "Road", Road);
            Each(_spec.rivers, nameof(MapSpec.rivers), "River", (river, name) =>
                river.crossings.ForEach(crossing => Crossing(river, crossing, name)));
            Each(_spec.woods, nameof(MapSpec.woods), "Wood", (wood, name) =>
                InClimate(Climate.woodlandKits, wood.kind, "woodland kit", name));
            Each(_spec.belts, nameof(MapSpec.belts), "Belt", (belt, name) =>
                InClimate(Climate.beltKits, belt.kind, "belt kit", name));
            Each(_spec.farmland, nameof(MapSpec.farmland), "Farmland", (farmland, name) =>
                InClimate(Climate.farmlandKits, farmland.kind, "farmland kit", name));
            Each(_spec.areas, nameof(MapSpec.areas), "Area", (area, name) => Surface(area.kind, name));
            Each(_spec.settlements, nameof(MapSpec.settlements), "Settlement", (settlement, name) =>
                Require(MapKeys.Contains(Palette.settlementKits, settlement.kind),
                    $"{name}: settlement kit '{settlement.kind}' is not in the palette."));
        }

        private void Header()
        {
            Require(_spec.size.x > 0f && _spec.size.z > 0f, $"Map size {_spec.size} must be positive.");
            var terrain = _spec.terrain;
            var rising = terrain.waterLevel < terrain.height && terrain.height < terrain.maxHeight;
            Require(rising, "Terrain heights must rise: waterLevel < height < maxHeight.");
            Require(Mathf.IsPowerOfTwo(terrain.resolution - 1), $"Terrain resolution {terrain.resolution} must be 2^n + 1.");
        }

        /// <summary>A line plants a climate tree or places a palette prop.</summary>
        private void Line(MapLineSpec line, string name)
        {
            var known = Climate.HasTree(line.kind) || MapKeys.Contains(Palette.entries, line.kind);
            Require(known, $"{name}: '{line.kind}' is neither a climate tree nor in the palette.");
        }

        private void Road(MapRoadSpec road, string name)
        {
            InClimate(Climate.roadClasses, road.kind, "road class", name);
            Require(road.points.Count >= 2, $"{name} needs at least two points.");
        }

        private void Crossing(MapRiverSpec river, MapCrossingSpec crossing, string name)
        {
            if (crossing.kind == MapCrossingSpec.Ford)
            {
                return;
            }

            if (crossing.kind != MapCrossingSpec.Bridge)
            {
                Issues.Error(
                    $"{name}: crossing kind '{crossing.kind}' is neither '{MapCrossingSpec.Bridge}' nor '{MapCrossingSpec.Ford}'.",
                    Feature);
                return;
            }

            if (!MapKeys.Contains(Palette.entries, crossing.prop))
            {
                Issues.Error($"{name}: bridge '{crossing.prop}' is not in the palette.", Feature);
                return;
            }

            var entry = Palette.Entry(crossing.prop);
            Require(entry.size.y >= river.width + BridgeOverhang,
                $"{name}: bridge '{crossing.prop}' ({entry.size.y} m) is too short for a {river.width} m river.");
            Require(entry.prefab == null || entry.IsEntity,
                $"{name}: bridge '{crossing.prop}' needs HyperRTS authoring (Destructible) to carry its deck.");
        }

        /// <summary>
        /// Checks every feature of the spec array <paramref name="array"/>, named the way issues name it ("Road 2")
        /// and reported against it.
        /// </summary>
        private void Each<T>(List<T> features, string array, string label, System.Action<T, string> check)
        {
            for (var i = 0; i < features.Count; i++)
            {
                Feature = new MapIssueFeature(array, i);
                check(features[i], $"{label} {i + 1}");
            }

            Feature = MapIssueFeature.None;
        }
    }
}
