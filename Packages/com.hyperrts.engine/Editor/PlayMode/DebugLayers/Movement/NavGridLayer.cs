using HyperRTS.Editor.Authoring;
using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers.Movement
{
    /// <summary>Nav grid cells: blocked (buildings, obstacles, slopes), water, and decks over water or gaps.</summary>
    public sealed class NavGridLayer : DebugLayer
    {
        private const float Alpha = 0.35f;

        public override string Label => "Nav grid";

        public override string Detail => "Red blocked cells, blue water, yellow decks over water or gaps.";

        public override string Group => "Movement";

        public override string Glyph => "grid-3x3";

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated)
            {
                return;
            }

            shapes.Cells(grid.Grid, new Colors
            {
                Cells = grid.Cells,
                Blocked = Tint(NavColors.Blocked),
                Water = Tint(NavColors.Water),
                Deck = Tint(NavColors.Deck),
            });
        }

        private static Color32 Tint(Color color) => new Color(color.r, color.g, color.b, Alpha);

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<byte> Cells;
            public Color32 Blocked;
            public Color32 Water;
            public Color32 Deck;

            public Color32 At(int index)
            {
                var surface = (NavSurface)Cells[index];
                if ((surface & NavSurface.Deck) != 0)
                {
                    return Deck;
                }

                if (surface == NavSurface.Blocked)
                {
                    return Blocked;
                }

                return surface == NavSurface.Water ? Water : default;
            }
        }
    }
}
