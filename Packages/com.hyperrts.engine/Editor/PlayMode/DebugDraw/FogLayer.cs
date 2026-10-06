using HyperRTS.Core;
using HyperRTS.Simulation.Vision;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Fog cells the local player's team currently sees.</summary>
    public sealed class FogLayer : CellDebugLayer
    {
        public override string Label => "Fog (visible to local)";

        // The local fog view only exists in the world the player looks at.
        public override WorldSystemFilterFlags Source => SimulationWorlds.Presented;

        public override void Draw(EntityManager entityManager)
        {
            var hasFog = TryGetSingleton(entityManager, out FogOfWar fog) && fog.IsCreated;
            if (!hasFog || !TryGetSingleton(entityManager, out LocalFogView local))
            {
                return;
            }

            BeginCells(new Color(0.3f, 1f, 0.4f, 0.2f));
            for (var i = 0; i < fog.Visible.Length; i++)
            {
                if (FogOfWar.HasTeam(fog.Visible[i], local.Team))
                {
                    Cell(fog.Min + (float2)fog.Cell(i) * fog.CellSize, fog.CellSize);
                }
            }

            EndCells();
        }
    }
}
