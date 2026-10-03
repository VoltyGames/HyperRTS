using Unity.Collections;

namespace HyperRTS.Presentation.Fog
{
    /// <summary>Converts fog-of-war team bitmasks into per-cell overlay opacity (0 = clear).</summary>
    public static class FogTexels
    {
        public static void Fill(NativeArray<byte> visible, NativeArray<byte> explored, byte team,
            byte exploredAlpha, byte unexploredAlpha, NativeArray<byte> output)
        {
            var mask = 1 << team;
            for (var i = 0; i < output.Length; i++)
            {
                output[i] = (visible[i] & mask) != 0 ? (byte)0
                    : (explored[i] & mask) != 0 ? exploredAlpha
                    : unexploredAlpha;
            }
        }
    }
}
