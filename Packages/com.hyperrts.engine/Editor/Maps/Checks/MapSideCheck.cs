using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;

namespace HyperRTS.Editor.Maps.Checks
{
    /// <summary>Checks the palette's sides: ids 1 to 255 listed once, kit pieces that are entity entries, build orders that load.</summary>
    public sealed class MapSideCheck : MapKeyCheck
    {
        public MapSideCheck(MapClimate climate, MapPalette palette, MapIssues issues) : base(climate, palette, issues)
        {
        }

        public void Check()
        {
            var ids = new HashSet<int>();
            foreach (var side in Palette.sides)
            {
                var name = $"Palette side {side.side}";
                Require(side.side >= 1 && side.side <= byte.MaxValue, $"{name}: side ids run from 1 to {byte.MaxValue}.");
                Require(ids.Add(side.side), $"{name} is listed twice.");
                side.startKit.ForEach(item => KitItem(item.key, $"{name} start kit"));
                var missing = !side.buildOrder.IsEmpty && side.buildOrderAsset == null;
                Require(!missing, $"{name}: build order '{side.buildOrder.path}' is not an AI Build Order asset.");
            }
        }

        private void KitItem(string key, string source)
        {
            Entry(key, source);
            if (!MapKeys.Contains(Palette.entries, key))
            {
                return;
            }

            var entry = Palette.Entry(key);
            Require(entry.prefab == null || entry.IsEntity, $"{source}: '{key}' is not an entity prefab, so it can't spawn in a match.");
        }
    }
}
