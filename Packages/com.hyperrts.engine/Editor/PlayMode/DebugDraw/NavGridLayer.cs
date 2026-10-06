using System;
using HyperRTS.Editor.Common;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Nav grid cells: blocked (buildings, obstacles, slopes), water, and decks over water or gaps.</summary>
    public sealed class NavGridLayer : CellDebugLayer
    {
        private const float Alpha = 0.35f;

        public override string Label => "Nav grid (blocked, water, decks)";

        public override void Draw(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated)
            {
                return;
            }

            DrawCells(grid, cell => cell == NavSurface.Blocked, NavColors.Blocked);
            DrawCells(grid, cell => cell == NavSurface.Water, NavColors.Water);
            DrawCells(grid, cell => (cell & NavSurface.Deck) != 0, NavColors.Deck);
        }

        private static void DrawCells(in NavGrid grid, Func<NavSurface, bool> include, Color color)
        {
            BeginCells(new Color(color.r, color.g, color.b, Alpha));
            for (var y = 0; y < grid.Size.y; y++)
            {
                for (var x = 0; x < grid.Size.x; x++)
                {
                    if (include(grid.Surface(new int2(x, y))))
                    {
                        Cell(grid.Min + new float2(x, y) * grid.CellSize, grid.CellSize);
                    }
                }
            }

            EndCells();
        }
    }
}
