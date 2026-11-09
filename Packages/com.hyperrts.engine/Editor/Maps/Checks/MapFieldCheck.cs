using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Kits;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Spec;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Checks every <see cref="MapKeyAttribute"/>, <see cref="AboveAttribute"/> and <c>[Min]</c> field of a spec, or of
    /// the climate and palette and their items, against the climate and palette a map is built with, naming the bad
    /// value by its path (<c>belts[1].kind 'Hedge' is not a belt in climate 'Steppe'</c>).
    /// </summary>
    public sealed class MapFieldCheck : MapCheck
    {
        private MapFieldCheck(MapClimate climate, MapPalette palette, MapIssues issues) : base(climate, palette, issues)
        {
        }

        public static void CheckSpec(MapSpec spec, MapClimate climate, MapPalette palette, MapIssues issues) =>
            MapLibraryFields.Walk(spec, new MapFieldCheck(climate, palette, issues).Field);

        public static void CheckLibrary(MapClimate climate, MapPalette palette, MapIssues issues)
        {
            var check = new MapFieldCheck(climate, palette, issues);
            foreach (var item in check.Items())
            {
                var name = $"{MapLibrary.TypeName(item.GetType())} '{item.key}'";
                MapLibraryFields.Walk(item, name, check.Field);
            }
        }

        private IEnumerable<MapLibraryItem> Items() =>
            new MapLibraryItem[] { Climate, Palette, Climate.war }
                .Concat(Climate.layers).Concat(Climate.surfaces).Concat(Climate.trees).Concat(Climate.plants)
                .Concat(Climate.rowCrops).Concat(Climate.decals).Concat(Climate.roadClasses)
                .Concat(Climate.farmlandKits).Concat(Climate.beltKits).Concat(Climate.woodlandKits)
                .Concat(Palette.entries).Concat(Palette.settlementKits)
                .Where(item => item != null);

        private void Field(FieldInfo field, string path, object value)
        {
            Feature = FeatureAt(path);
            if (value is string key)
            {
                Key(field, path, key);
            }
            else
            {
                Number(field, path, value);
            }
        }

        private void Number(FieldInfo field, string path, object value)
        {
            var number = Convert.ToDouble(value);
            if (field.GetCustomAttribute<AboveAttribute>() is { } above)
            {
                Require(number > above.Limit, $"{path} {value} must be above {above.Limit}.");
            }

            if (field.GetCustomAttribute<MinAttribute>() is { } min)
            {
                Require(number >= min.min, $"{path} {value} must be at least {min.min}.");
            }
        }

        private void Key(FieldInfo field, string path, string key)
        {
            var attribute = field.GetCustomAttribute<MapKeyAttribute>();
            if (attribute == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(key))
            {
                var names = string.Join(" or ", attribute.Types.Select(MapLibrary.TypeName));
                Require(attribute.Optional, $"{path} names no {names}.");
                return;
            }

            if (!attribute.Types.Any(type => Scope(type).Items.Any(item => item.Key == key)))
            {
                var places = attribute.Types.Select(type => $"a {MapLibrary.TypeName(type)} in {Scope(type).Place}");
                Issues.Error($"{path} '{key}' is not {string.Join(" or ", places)}.", Feature);
            }
        }

        // The climate's and the palette's own lists, not the whole library: a spec builds with one of each.
        private (IEnumerable<IMapKeyed> Items, string Place) Scope(Type type) => type.Name switch
        {
            nameof(MapClimate) => (new IMapKeyed[] { Climate }, "the map library"),
            nameof(MapClimateSurface) => (Climate.surfaces, InClimate),
            nameof(MapClimateLayer) => (Climate.layers, InClimate),
            nameof(MapClimatePlant) => (Climate.plants, InClimate),
            nameof(MapClimateDecal) => (Climate.decals, InClimate),
            nameof(MapClimateRowCrop) => (Climate.rowCrops, InClimate),
            nameof(MapWarKit) => (new[] { Climate.war }.Where(war => war != null), InClimate),
            nameof(MapClimateTree) => (Climate.trees, InClimate),
            nameof(MapRoadClass) => (Climate.roadClasses, InClimate),
            nameof(MapFarmlandKit) => (Climate.farmlandKits, InClimate),
            nameof(MapBeltKit) => (Climate.beltKits, InClimate),
            nameof(MapWoodlandKit) => (Climate.woodlandKits, InClimate),
            nameof(MapProp) => (Palette.entries, InPalette),
            nameof(MapSettlementKit) => (Palette.settlementKits, InPalette),
            _ => throw new ArgumentException($"HyperRTS: a map spec can't name a {type.Name}; the climate and palette hold no such list."),
        };

        private string InClimate => $"climate '{Climate.key}'";

        private string InPalette => $"palette '{Palette.key}'";

        /// <summary>The top-level feature a path lies in (<c>rivers[0]</c> of <c>rivers[0].crossings[1].prop</c>).</summary>
        private static MapIssueFeature FeatureAt(string path)
        {
            var open = path.IndexOf('[');
            if (open < 0)
            {
                return MapIssueFeature.None;
            }

            var dot = path.IndexOf('.');
            if (dot >= 0 && dot < open)
            {
                return MapIssueFeature.None;
            }

            var close = path.IndexOf(']', open);
            return new MapIssueFeature(path[..open], int.Parse(path[(open + 1)..close]));
        }
    }
}
