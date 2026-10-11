using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Converts fog-of-war team bitmasks into per-cell overlay opacity (0 = clear).</summary>
    public static class FogTexels
    {
        public static void Fill(NativeArray<ushort> visible, NativeArray<ushort> explored, byte team,
            byte exploredAlpha, byte unexploredAlpha, NativeArray<byte> output)
        {
            new FillJob
            {
                Visible = visible,
                Explored = explored,
                Mask = 1 << team,
                ExploredAlpha = exploredAlpha,
                UnexploredAlpha = unexploredAlpha,
                Output = output,
            }.Run();
        }

        [BurstCompile]
        private struct FillJob : IJob
        {
            [ReadOnly] public NativeArray<ushort> Visible;
            [ReadOnly] public NativeArray<ushort> Explored;
            public int Mask;
            public byte ExploredAlpha;
            public byte UnexploredAlpha;
            public NativeArray<byte> Output;

            public void Execute()
            {
                for (var i = 0; i < Output.Length; i++)
                {
                    Output[i] = (Visible[i] & Mask) != 0 ? (byte)0
                        : (Explored[i] & Mask) != 0 ? ExploredAlpha
                        : UnexploredAlpha;
                }
            }
        }
    }
}
