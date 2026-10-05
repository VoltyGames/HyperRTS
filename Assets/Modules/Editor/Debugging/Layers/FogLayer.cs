using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.Debugging.Layers
{
    /// <summary>Fog cells the local player's team currently sees.</summary>
    public sealed class FogLayer : CellDebugLayer
    {
        public override string Label => "Fog (visible to local)";

        public override void Draw(EntityManager entityManager)
        {
            if (!TryGetSingleton(entityManager, out FogOfWar fog) || !fog.IsCreated ||
                !TryGetSingleton(entityManager, out LocalFogView local))
            {
                return;
            }

            BeginCells(new Color(0.3f, 1f, 0.4f, 0.2f));
            for (var i = 0; i < fog.Visible.Length; i++)
            {
                if (FogOfWar.HasTeam(fog.Visible[i], local.Team))
                {
                    Cell(fog.Min + new float2(i % fog.Size.x, i / fog.Size.x) * fog.CellSize, fog.CellSize);
                }
            }

            EndCells();
        }
    }
}
