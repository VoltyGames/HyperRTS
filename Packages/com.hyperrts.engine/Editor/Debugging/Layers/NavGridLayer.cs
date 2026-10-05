using HyperRTS.Simulation.Navigation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Debugging.Layers
{
    /// <summary>Nav grid cells blocked by buildings and obstacles.</summary>
    public sealed class NavGridLayer : CellDebugLayer
    {
        public override string Label => "Nav grid (blocked)";

        public override void Draw(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out NavGrid grid) || !grid.IsCreated)
            {
                return;
            }

            BeginCells(new Color(1f, 0.2f, 0.2f, 0.35f));
            for (var y = 0; y < grid.Size.y; y++)
            {
                for (var x = 0; x < grid.Size.x; x++)
                {
                    if (!grid.IsWalkable(new int2(x, y)))
                    {
                        Cell(grid.Min + new float2(x, y) * grid.CellSize, grid.CellSize);
                    }
                }
            }

            EndCells();
        }
    }
}
