using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Remains that last <see cref="Lifetime"/> seconds, then sink <see cref="DespawnDepth"/> metres and go.</summary>
    public struct Decay : IComponentData
    {
        public float Lifetime;

        public float DespawnDuration;

        public float DespawnDepth;

        /// <summary>Elapsed time the despawn starts, set by the authoritative world on its first look; 0 until then.</summary>
        public double DespawnStartTime;

        /// <summary>Share of the despawn done, 0 to 1. Replicated so clients fade the remains' loop effects with it.</summary>
        [GhostField(Quantization = 100)] public float DespawnProgress;
    }
}
