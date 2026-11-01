using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;
using HyperRTS.Editor.Maps.Spec;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Checks every <see cref="MapKeyAttribute"/> field of a spec against the climate or palette it is built with,
    /// naming the bad key by its JSON path (<c>belts[1].kind 'Hedge' is not a belt in climate 'Steppe'</c>).
    /// </summary>
    public sealed class MapSpecKeyCheck : MapKeyCheck
    {
        private MapSpecKeyCheck(MapClimate climate, MapPalette palette, MapIssues issues) : base(climate, palette, issues)
        {
        }

        public static void Check(MapSpec spec, MapClimate climate, MapPalette palette, MapIssues issues) =>
            MapLibraryFields.Walk(spec, new MapSpecKeyCheck(climate, palette, issues).Key);

        private void Key(FieldInfo field, string path, string key)
        {
            var attribute = field.GetCustomAttribute<MapKeyAttribute>();
            if (attribute == null)
            {
                return;
            }

            Feature = FeatureAt(path);
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
