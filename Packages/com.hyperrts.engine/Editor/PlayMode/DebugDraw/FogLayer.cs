using HyperRTS.Core;
using HyperRTS.Simulation.Vision;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugDraw
{
    /// <summary>Fog cells the local player's team currently sees.</summary>
    public sealed class FogLayer : CellTextureLayer
    {
        public override string Label => "Fog (visible to local)";

        // The local fog view only exists in the world the player looks at.
        public override WorldSystemFilterFlags Source => SimulationWorlds.Presented;

        protected override bool Paint(EntityManager entityManager, CellTexture texture)
        {
            var hasFog = TryGetSingleton(entityManager, out FogOfWar fog) && fog.IsCreated;
            if (!hasFog || !TryGetSingleton(entityManager, out LocalFogView local))
            {
                return false;
            }

            texture.Paint(fog.Grid, new Colors
            {
                Visible = fog.Visible,
                PlaneStart = local.Team * fog.PlaneWords,
                RowWords = fog.RowWords,
                Width = fog.Grid.Size.x,
            });
            return true;
        }

        private struct Colors : ICellColors
        {
            [ReadOnly] public NativeArray<ulong> Visible;
            public int PlaneStart;
            public int RowWords;
            public int Width;

            public Color32 At(int index)
            {
                var x = index % Width;
                var word = Visible[PlaneStart + index / Width * RowWords + (x >> 6)];
                return (word & (1ul << (x & 63))) != 0 ? new Color32(77, 255, 102, 50) : default;
            }
        }
    }
}
