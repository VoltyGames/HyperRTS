using HyperRTS.Editor.PlayMode.DebugLayers;
using HyperRTS.Simulation.Common;
using HyperRTS.Simulation.Vision.Fog;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Editor.PlayMode.DebugLayers.Vision
{
    /// <summary>Fog cells the local player's team currently sees.</summary>
    public sealed class FogLayer : DebugLayer
    {
        public override string Label => "Fog";

        public override string Detail => "Green where the local player's team sees.";

        public override string Group => "Vision";

        public override string Glyph => "cloud-fog";

        // The local fog view only exists in the world the player looks at.
        public override WorldSystemFilterFlags Source => SimulationWorlds.Presented;

        public override void Draw(EntityManager entityManager, DebugShapes shapes)
        {
            if (!TryGetSingleton(entityManager, out FogOfWar fog) || !fog.IsCreated
                || !TryGetSingleton(entityManager, out LocalFogView local))
            {
                return;
            }

            shapes.Cells(fog.Grid, new Colors
            {
                Visible = fog.Visible,
                PlaneStart = local.Team * fog.PlaneWords,
                RowWords = fog.RowWords,
                Width = fog.Grid.Size.x,
            });
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
