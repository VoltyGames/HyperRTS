using HyperRTS.Simulation.Navigation.Grid;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Nav grid cells: blocked (buildings, obstacles, slopes), water, and decks over water or gaps.</summary>
    public sealed class NavGridLayer : CellTextureLayer
    {
        private const float Alpha = 0.35f;

        public override string Label => "Nav grid (blocked, water, decks)";

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated)
            {
                return false;
            }

            texture.Paint(grid.Grid, new Colors
            {
                Cells = grid.Cells,
                Blocked = Tint(NavColors.Blocked),
                Water = Tint(NavColors.Water),
                Deck = Tint(NavColors.Deck),
            });
            return true;
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
