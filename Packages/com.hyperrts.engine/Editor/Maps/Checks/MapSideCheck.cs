using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>Checks the palette's sides: ids 1 to 255 listed once, kit pieces that are entity entries, build orders that load.</summary>
    public static class MapSideCheck
    {
        public static void Check(MapPalette palette, MapIssues issues)
        {
            var ids = new HashSet<int>();
            foreach (var side in palette.sides)
            {
                var name = $"Palette side {side.side}";
                if (side.side < 1 || side.side > byte.MaxValue)
                {
                    issues.Error($"{name}: side ids run from 1 to {byte.MaxValue}.");
                }

                if (!ids.Add(side.side))
                {
                    issues.Error($"{name} is listed twice.");
                }

                side.startKit.ForEach(item => KitItem(palette, item.key, name, issues));
                var missing = !string.IsNullOrEmpty(side.buildOrder) && side.buildOrderAsset == null;
                if (missing)
                {
                    issues.Error($"{name}: build order '{side.buildOrder}' is not an AI Build Order asset.");
                }
            }
        }

        private static void KitItem(MapPalette palette, string key, string name, MapIssues issues)
        {
            if (!MapKeys.Contains(palette.entries, key))
            {
                issues.Error($"{name} start kit: '{key}' is not in the palette.");
                return;
            }

            var prefab = palette.Entry(key).prefab;
            if (prefab != null && !palette.Entry(key).IsEntity)
            {
                issues.Error($"{name} start kit: '{key}' is not an entity prefab, so it can't spawn in a match.");
            }
        }
    }
}
