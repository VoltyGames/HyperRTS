using System.Collections.Generic;

namespace HyperRTS.Editor.Maps.Library
{
    /// <summary>
    /// Key lookups over climate, palette and rules lists. The map checks report every key that doesn't resolve before
    /// a build plans anything, so a lookup after them that misses is a bug and throws.
    /// </summary>
    public static class MapKeys
    {
        public static bool Contains<T>(IReadOnlyList<T> items, string key) where T : IMapKeyed => Find(items, key) >= 0;

        public static T Get<T>(IReadOnlyList<T> items, string key, string owner, string what) where T : IMapKeyed =>
            items[RequireIndex(items, key, owner, what)];

        public static int RequireIndex<T>(IReadOnlyList<T> items, string key, string owner, string what) where T : IMapKeyed
        {
            var index = Find(items, key);
            return index >= 0
                ? index
                : throw new KeyNotFoundException($"HyperRTS: '{owner}' has no {what} '{key}'; the map checks should have caught it.");
        }

        private static int Find<T>(IReadOnlyList<T> items, string key) where T : IMapKeyed
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
