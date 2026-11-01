using HyperRTS.Editor.Maps.Common;
using HyperRTS.Simulation.Common;
using System;
using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Checks a spec before it is refined: its size, terrain and amounts are sane and every kind it names is in the
    /// climate, the palette (through <see cref="MapSpecKeys"/>) or the rules. Where features lie (sector, bounds,
    /// symmetry) is the expander's to check.
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
            MapSpecKeys.Check(_spec, Climate, Palette, Issues);
            Require(MapKeys.Contains(_rules.modes, _spec.mode), $"The map rules have no thresholds for mode '{_spec.mode}'.");
            Each(_spec.resources, nameof(MapSpec.resources), "Resource", (resource, name) =>
                Require(resource.amount > 0, $"{name} ('{resource.kind}') needs an amount above 0."));
            Each(_spec.props, nameof(MapSpec.props), "Prop", (prop, name) => NotBesideRoad(prop.kind, name));
            Each(_spec.roads, nameof(MapSpec.roads), "Road", (road, name) =>
                Require(road.points.Count >= 2, $"{name} needs at least two points."));
            Each(_spec.rivers, nameof(MapSpec.rivers), "River", (river, name) =>
                river.crossings.ForEach(crossing => Crossing(river, crossing, name)));
        }

        private void Header()
        {
            Require(_spec.size.x > 0f && _spec.size.z > 0f, $"Map size {_spec.size} must be positive.");
            var terrain = _spec.terrain;
            var rising = terrain.waterLevel < terrain.height && terrain.height < terrain.maxHeight;
            Require(rising, "Terrain heights must rise: waterLevel < height < maxHeight.");
            Require(Mathf.IsPowerOfTwo(terrain.resolution - 1), $"Terrain resolution {terrain.resolution} must be 2^n + 1.");
            Weather();
            WarAge();
        }

        private void WarAge()
        {
            if (Array.IndexOf(Enum.GetNames(typeof(MapWarAge)), _spec.warAge) < 0)
            {
                Issues.Error($"The map spec's warAge '{_spec.warAge}' is not one of {string.Join(", ", Enum.GetNames(typeof(MapWarAge)))}.", Feature);
                return;
            }

            Require(_spec.WarAge == MapWarAge.None || Climate.war != null,
                $"warAge '{_spec.warAge}' needs climate '{Climate.key}' to name a warKit.");
        }

        private void Weather()
        {
            if (string.IsNullOrEmpty(_spec.weather))
            {
                Issues.Error($"The map spec's 'weather' key is missing; name one of climate '{Climate.key}''s weathers.", Feature);
                return;
            }

            InClimate(Climate.weathers, _spec.weather, "weather", "The map spec's 'weather' key");
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

            if (string.IsNullOrEmpty(crossing.prop))
            {
                Issues.Error($"{name}: a bridge crossing needs a prop.", Feature);
                return;
            }

            // MapSpecKeys reports a prop that isn't in the palette.
            if (!MapKeys.Contains(Palette.entries, crossing.prop))
            {
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
