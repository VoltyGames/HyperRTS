using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning
{
    /// <summary>
    /// A settlement kit with every key resolved: palette entries, the orchard tree and the checked surfaces. A kit
    /// field left empty means the kit has none; a key that is set but unknown is an error naming kit and key.
    /// </summary>
    internal sealed class MapSettlementEntries
    {
        private MapSettlementEntries()
        {
        }

        public List<MapPaletteEntry> Houses { get; } = new();

        public List<MapPaletteEntry> Outbuildings { get; } = new();

        public List<(MapPaletteEntry Entry, MapPlotProp Prop)> Extras { get; } = new();

        public MapPaletteEntry FrontFence { get; private set; }

        public MapPaletteEntry SideFence { get; private set; }

        public MapPaletteEntry Gate { get; private set; }

        public MapPaletteEntry Pipe { get; private set; }

        public MapPaletteEntry Pole { get; private set; }

        /// <summary>Climate tree index of the orchard, -1 for a meadow back.</summary>
        public int OrchardTree { get; private set; } = -1;

        /// <summary>Extra yard behind the house that outbuildings stand in.</summary>
        public float Strip { get; private set; }

        /// <summary>The resolved kit, or null (with errors reported) when anything doesn't resolve.</summary>
        public static MapSettlementEntries Resolve(MapPlanContext context, MapSettlementKit kit)
        {
            var errors = context.Issues.Errors.Count;
            var entries = new MapSettlementEntries();
            entries.Load(context, kit, $"settlement kit '{kit.key}'");
            return context.Issues.Errors.Count == errors ? entries : null;
        }

        private void Load(MapPlanContext context, MapSettlementKit kit, string source)
        {
            CheckValues(context, kit, source);
            Fill(context, Houses, kit.houses, source);
            Fill(context, Outbuildings, kit.outbuildings, source);
            FrontFence = Optional(context, kit.frontFence, source);
            SideFence = Optional(context, kit.sideFence, source);
            Gate = Optional(context, kit.gate, source);
            Pipe = Optional(context, kit.streetPipe, source);
            Pole = Optional(context, kit.streetPole, source);
            foreach (var extra in kit.extras)
            {
                var entry = context.Placer.Entry(extra.kind, source);
                if (entry != null)
                {
                    Extras.Add((entry, extra));
                }
            }

            if (!string.IsNullOrEmpty(kit.orchardTree))
            {
                OrchardTree = context.Tree(kit.orchardTree, source);
            }

            Strip = OutbuildingStrip(kit);
        }

        private static void CheckValues(MapPlanContext context, MapSettlementKit kit, string source)
        {
            if (kit.houses.Count == 0)
            {
                context.Issues.Error($"{source} has no houses.");
            }

            if (kit.plot.x < 1f || kit.plot.y < 1f)
            {
                context.Issues.Error($"{source}: plot {kit.plot} must be at least 1 m each way.");
            }

            if (!string.IsNullOrEmpty(kit.orchardTree) && kit.orchardSpacing < 1f)
            {
                context.Issues.Error($"{source}: orchardSpacing {kit.orchardSpacing} must be at least 1 m.");
            }

            if (!string.IsNullOrEmpty(kit.streetPole) && kit.poleSpacing < 1f)
            {
                context.Issues.Error($"{source}: poleSpacing {kit.poleSpacing} must be at least 1 m.");
            }

            foreach (var surface in new[] { kit.yardSurface, kit.gardenSurface, kit.backSurface })
            {
                if (context.Climate.Surface(surface) == null)
                {
                    context.Issues.Error($"{source}: surface '{surface}' is not in climate '{context.Climate.name}'.");
                }
            }
        }

        private static void Fill(MapPlanContext context, List<MapPaletteEntry> list, List<string> keys, string source)
        {
            foreach (var key in keys)
            {
                var entry = context.Placer.Entry(key, source);
                if (entry != null)
                {
                    list.Add(entry);
                }
            }
        }

        private static MapPaletteEntry Optional(MapPlanContext context, string key, string source) =>
            string.IsNullOrEmpty(key) ? null : context.Placer.Entry(key, source);

        private float OutbuildingStrip(MapSettlementKit kit)
        {
            if (kit.maxOutbuildings <= 0)
            {
                return 0f;
            }

            var deepest = 0f;
            foreach (var entry in Outbuildings)
            {
                deepest = Mathf.Max(deepest, entry.size.y * entry.scale.y);
            }

            return deepest > 0f ? deepest + 1f : 0f;
        }
    }
}
