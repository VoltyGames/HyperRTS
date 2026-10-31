using System.IO;
using System.Linq;
using System.Text;
using HyperRTS.Editor.Maps.Climate;
using HyperRTS.Editor.Maps.Library;
using HyperRTS.Editor.Maps.Palette;

namespace HyperRTS.Editor.Maps.Tools
{
    /// <summary>Writes every key a map spec can name as a Markdown reference for spec authors.</summary>
    public static class MapKeyReference
    {
        public static void Write(string path, MapLibrary library) => File.WriteAllText(path, Text(library));

        /// <summary>The reference <see cref="Write"/> writes, so a test can compare it with the committed file.</summary>
        public static string Text(MapLibrary library)
        {
            var climates = library.Keys<MapClimate>().Select(key => library.Climate(key, null)).ToArray();
            return Text(library.Palette(), climates);
        }

        public static string Text(MapPalette palette, params MapClimate[] climates)
        {
            var text = new StringBuilder();
            text.AppendLine("# Map keys");
            text.AppendLine();
            text.AppendLine("Generated from the map library whenever an item changes; don't edit. Every `kind` a spec names must be listed here.");
            foreach (var climate in climates)
            {
                Climate(text, climate);
                text.AppendLine().AppendLine($"Weathers (`weather`): {string.Join(", ", climate.weathers.Select(w => $"`{w.key}`"))}.");
            }

            Palette(text, palette);
            // LF on every platform, so the committed file doesn't churn.
            return text.ToString().Replace("\r\n", "\n");
        }

        private static void Climate(StringBuilder text, MapClimate climate)
        {
            text.AppendLine().AppendLine($"## Climate `{climate.key}`").AppendLine();
            text.AppendLine("| Road class (`roads[].kind`) | Width | Surface | Verge | Rank | Levelled |").AppendLine("| --- | --- | --- | --- | --- | --- |");
            foreach (var road in climate.roadClasses)
            {
                text.AppendLine($"| `{road.key}` | {road.width} m | {road.surface} | {road.vergeWidth} m {road.vergeSurface} | {road.rank} | {(road.level ? "yes" : "no")} |");
            }

            text.AppendLine().AppendLine("| Farmland kit (`farmland[].kind`) | Field | Crops | Belts | Tracks |").AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var kit in climate.farmlandKits)
            {
                var crops = string.Join(", ", kit.crops.Select(c => $"{c.surface} ×{c.weight}"));
                text.AppendLine($"| `{kit.key}` | {kit.fieldSize.x} × {kit.fieldSize.y} m | {crops} | {kit.beltKit} | {kit.trackClass} |");
            }

            text.AppendLine().AppendLine("| Belt kit (`belts[].kind`) | Width | Rows | Trees |").AppendLine("| --- | --- | --- | --- |");
            foreach (var kit in climate.beltKits)
            {
                text.AppendLine($"| `{kit.key}` | {kit.width} m | {kit.rows} | {string.Join(", ", kit.trees.Select(t => t.tree))} |");
            }

            text.AppendLine().AppendLine("| Woodland kit (`woods[].kind`) | Floor | Trees per ha |").AppendLine("| --- | --- | --- |");
            foreach (var kit in climate.woodlandKits)
            {
                text.AppendLine($"| `{kit.key}` | {kit.surface} | {string.Join(", ", kit.trees.Select(t => $"{t.tree} {t.perHectare}"))} |");
            }

            text.AppendLine().AppendLine("| Surface (`areas[].kind`) | Layer | Plants | Row crop | Trees per ha |")
                .AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var surface in climate.surfaces)
            {
                var plants = string.Join(", ", surface.plants.Select(d => d.plant));
                var trees = string.Join(", ", surface.trees.Select(t => $"{t.tree} {t.perHectare}"));
                text.AppendLine($"| `{surface.key}` | {surface.layer} | {plants} | {surface.rowCrop} | {trees} |");
            }

            text.AppendLine().AppendLine($"Trees (`rows[].kind` plants a row): {string.Join(", ", climate.trees.Select(t => $"`{t.key}`"))}.");
        }

        private static void Palette(StringBuilder text, MapPalette palette)
        {
            text.AppendLine().AppendLine("## Palette").AppendLine();
            text.AppendLine("| Entry (`props`, `resources`, `rows`, `scatters`, bridges) | Footprint | Kind | Pad | Access |").AppendLine("| --- | --- | --- | --- | --- |");
            foreach (var entry in palette.entries.Where(e => e.prefab != null))
            {
                text.AppendLine($"| `{entry.key}` | {entry.size.x} × {entry.size.y} m | {Kind(entry)} | {entry.pad} | {entry.access} |");
            }

            text.AppendLine().AppendLine("| Settlement kit (`settlements[].kind`) | Plot | Houses | Outbuildings |").AppendLine("| --- | --- | --- | --- |");
            foreach (var kit in palette.settlementKits)
            {
                text.AppendLine($"| `{kit.key}` | {kit.plot.x} × {kit.plot.y} m | {string.Join(", ", kit.houses)} | {string.Join(", ", kit.outbuildings)} |");
            }
        }

        private static string Kind(MapProp entry)
        {
            if (entry.IsGameplay)
            {
                return "gameplay";
            }

            return entry.IsEntity ? "destructible" : "scenery";
        }
    }
}
