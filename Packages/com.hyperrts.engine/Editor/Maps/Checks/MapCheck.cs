using System.Collections.Generic;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Common;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Base of the map checks: holds the climate, palette and issues they report to. <see cref="MapFieldCheck"/>
    /// reports each key that doesn't resolve, so everything after can look keys up without checking them again.
    /// </summary>
    public abstract class MapCheck
    {
        protected MapCheck(MapClimate climate, MapPalette palette, MapIssues issues)
        {
            Climate = climate;
            Palette = palette;
            Issues = issues;
        }

        protected MapClimate Climate { get; }

        protected MapPalette Palette { get; }

        protected MapIssues Issues { get; }

        /// <summary>The spec feature being checked; errors are reported against it.</summary>
        protected MapIssueFeature Feature { get; set; }

        protected void Require(bool condition, string message)
        {
            if (!condition)
            {
                Issues.Error(message, Feature);
            }
        }

        protected void InClimate<T>(IReadOnlyList<T> items, string key, string what, string source) where T : IMapKeyed =>
            Require(MapKeys.Contains(items, key), $"{source}: {what} '{key}' is not in climate '{Climate.key}'.");

        /// <summary>A prop placed where besideRoad would be ignored: only scatters turn props onto a verge.</summary>
        protected void NotBesideRoad(string key, string source)
        {
            if (!MapKeys.Contains(Palette.entries, key))
            {
                return;
            }

            Require(!Palette.Entry(key).besideRoad, $"{source}: '{key}' sets besideRoad, which only scatters honour.");
        }

        /// <summary>Checks a key a kit may leave empty for "none".</summary>
        protected static void Optional(string key, System.Action<string> check)
        {
            if (!string.IsNullOrEmpty(key))
            {
                check(key);
            }
        }
    }
}
