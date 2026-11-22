using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Presentation.Rendering
{
    /// <summary>
    /// Steps the global _HyperRTSDitherSeed every rendered frame, so Dither.hlsl's pattern moves and the temporal
    /// upscaler averages dithered fades smooth.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct DitherSeedSystem : ISystem
    {
        private static readonly int SeedId = Shader.PropertyToID("_HyperRTSDitherSeed");

        // A short cycle keeps the noise's pixel offsets small enough for float precision.
        private const int Cycle = 64;

        private int _seed;

        public void OnUpdate(ref SystemState state)
        {
            _seed = (_seed + 1) % Cycle;
            Shader.SetGlobalFloat(SeedId, _seed);
        }
    }
}
