using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Remains that last <see cref="Lifetime"/> seconds, then sink <see cref="SinkDepth"/> metres and go.</summary>
    public struct Decay : IComponentData
    {
        /// <summary>Seconds left before sinking starts; counted down by the authoritative world only.</summary>
        public float Lifetime;

        public float SinkDuration;

        public float SinkDepth;

        /// <summary>Share of the sink done, 0 to 1. Replicated so clients fade the remains' loop effects with it.</summary>
        [GhostField(Quantization = 100)] public float Sunk;
    }
}
