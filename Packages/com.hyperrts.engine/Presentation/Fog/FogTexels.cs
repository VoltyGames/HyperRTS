using HyperRTS.Simulation.Vision;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Converts a team's fog-of-war planes into per-cell overlay opacity (0 = clear), from the ground layer.</summary>
    public static class FogTexels
    {
        public static void Fill(in FogOfWar fog, byte team, byte exploredAlpha, byte unexploredAlpha,
            NativeArray<byte> output)
        {
            new FillJob
            {
                Fog = fog,
                Team = team,
                ExploredAlpha = exploredAlpha,
                UnexploredAlpha = unexploredAlpha,
                Output = output,
            }.Run();
        }

        [BurstCompile]
        private struct FillJob : IJob
        {
            [ReadOnly] public FogOfWar Fog;
            public byte Team;
            public byte ExploredAlpha;
            public byte UnexploredAlpha;
            public NativeArray<byte> Output;

            public void Execute()
            {
                for (var y = 0; y < Fog.Frame.Size.y; y++)
                {
                    for (var x = 0; x < Fog.Frame.Size.x; x++)
                    {
                        Output[y * Fog.Frame.Size.x + x] = Alpha(new int2(x, y));
                    }
                }
            }

            private readonly byte Alpha(int2 cell)
            {
                if (Fog.Has(Fog.Visible, cell, Team))
                {
                    return 0;
                }

                return Fog.Has(Fog.Explored, cell, Team) ? ExploredAlpha : UnexploredAlpha;
            }
        }
    }
}
