using System;
using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Debugging.Layers
{
    /// <summary>Nav grid cells: blocked (buildings, obstacles, slopes), water, and decks over water or gaps.</summary>
    public sealed class NavGridLayer : CellDebugLayer
    {
        public override string Label => "Nav grid (blocked, water, decks)";

        public override void Draw(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated)
            {
                return;
            }

            DrawCells(grid, cell => cell == NavSurface.Blocked, new Color(1f, 0.2f, 0.2f, 0.35f));
            DrawCells(grid, cell => cell == NavSurface.Water, new Color(0.2f, 0.45f, 1f, 0.35f));
            DrawCells(grid, cell => (cell & NavSurface.Deck) != 0, new Color(1f, 0.8f, 0.2f, 0.35f));
        }

        private static void DrawCells(in NavGrid grid, Func<NavSurface, bool> include, Color color)
        {
            BeginCells(color);
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
