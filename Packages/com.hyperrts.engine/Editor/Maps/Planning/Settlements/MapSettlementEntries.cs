using System.Collections.Generic;
using HyperRTS.Editor.Maps.Data;
using UnityEngine;

namespace HyperRTS.Editor.Maps.Planning.Settlements
{
    /// <summary>A settlement kit with its keys resolved to palette entries and the orchard tree; empty keys stay null.</summary>
    internal sealed class MapSettlementEntries
    {
        public MapSettlementEntries(MapPalette palette, MapClimate climate, MapSettlementKit kit)
        {
            Houses = kit.houses.ConvertAll(palette.Entry);
            Outbuildings = kit.outbuildings.ConvertAll(palette.Entry);
            Extras = kit.extras.ConvertAll(extra => (palette.Entry(extra.kind), extra));
            FrontFence = Optional(palette, kit.frontFence);
            SideFence = Optional(palette, kit.sideFence);
            Gate = Optional(palette, kit.gate);
            Pipe = Optional(palette, kit.streetPipe);
            PipeArch = Optional(palette, kit.streetPipeArch);
            Pole = Optional(palette, kit.streetPole);
            OrchardTree = string.IsNullOrEmpty(kit.orchardTree) ? -1 : climate.TreeIndex(kit.orchardTree);
            OutbuildingDepth = DeepestOutbuilding(kit, Outbuildings);
        }

        public List<MapPaletteEntry> Houses { get; }

        public List<MapPaletteEntry> Outbuildings { get; }

        public List<(MapPaletteEntry Entry, MapPlotProp Prop)> Extras { get; }

        public MapPaletteEntry FrontFence { get; }

        public MapPaletteEntry SideFence { get; }

        public MapPaletteEntry Gate { get; }

        public MapPaletteEntry Pipe { get; }

        /// <summary>The arch carrying the pipe over a gap; the asset check requires it whenever there is a pipe.</summary>
        public MapPaletteEntry PipeArch { get; }

        public MapPaletteEntry Pole { get; }

        /// <summary>Climate tree index of the orchard, -1 for a meadow back.</summary>
        public int OrchardTree { get; }

        /// <summary>Extra yard behind the house that outbuildings stand in.</summary>
        public float OutbuildingDepth { get; }

        private static MapPaletteEntry Optional(MapPalette palette, string key) =>
            string.IsNullOrEmpty(key) ? null : palette.Entry(key);

        private static float DeepestOutbuilding(MapSettlementKit kit, List<MapPaletteEntry> outbuildings)
        {
            if (kit.maxOutbuildings <= 0)
            {
                return 0f;
            }

            var deepest = 0f;
            foreach (var entry in outbuildings)
            {
                deepest = Mathf.Max(deepest, entry.size.y * entry.scale.y);
            }

            return deepest > 0f ? deepest + 1f : 0f;
        }
    }
}
