using HyperRTS.Simulation.Cues.Surfaces;
using Unity.Entities;

namespace HyperRTS.Presentation.Effects.Streams
{
    /// <summary>Dust a rotorcraft's downwash raises from the ground under it, by the ground material and its height.</summary>
    public struct RotorWash : IComponentData
    {
        public UnityObjectRef<SurfaceEffects> Effects;

        /// <summary>Metres above the ground from which no dust rises; full dust at the ground.</summary>
        public float Height;

        /// <summary>The authored default, also the downwash reach of rotors with no wash.</summary>
        public const float DefaultHeight = 25f;
    }
}
