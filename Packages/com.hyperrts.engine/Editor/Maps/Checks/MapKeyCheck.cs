using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>
    /// Reports each key that doesn't resolve with where it was named, so everything after the checks can look keys up
    /// without checking them again.
    /// </summary>
    public abstract class MapKeyCheck
    {
        protected MapKeyCheck(MapClimate climate, MapPalette palette, MapIssues issues)
        {
            Climate = climate;
            Palette = palette;
            Issues = issues;
        }

        protected MapClimate Climate { get; }

        protected MapPalette Palette { get; }

        protected MapIssues Issues { get; }

        protected void Require(bool condition, string message)
        {
            if (!condition)
            {
                Issues.Error(message);
            }
        }

        protected void Surface(string key, string source) => InClimate(Climate.surfaces, key, "surface", source);

        protected void Tree(string key, string source) => InClimate(Climate.trees, key, "tree", source);

        protected void InClimate<T>(IReadOnlyList<T> items, string key, string what, string source) where T : IMapKeyed =>
            Require(MapKeys.Contains(items, key), $"{source}: {what} '{key}' is not in climate '{Climate.name}'.");

        protected void Entry(string key, string source) =>
            Require(MapKeys.Contains(Palette.entries, key), $"{source}: '{key}' is not in the palette.");

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
