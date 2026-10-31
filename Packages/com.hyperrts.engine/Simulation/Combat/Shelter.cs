using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// A container whose walls stop part of each hit before it reaches the passengers inside. The share that gets
    /// through goes to its <see cref="ShelterHit"/> buffer for the container to hand out.
    /// </summary>
    public struct Shelter : IComponentData
    {
        /// <summary>Share of a hit the intact walls stop: 0 stops nothing, 1 everything.</summary>
        public float Protection;

        /// <summary>
        /// The share of <paramref name="hit"/> that reaches the passengers: <paramref name="amount"/> past the
        /// protection that is left at <paramref name="intact"/> health, scaled by the damage type's garrison factor.
        /// </summary>
        public readonly float Passing(in DamageEvent hit, float amount, float intact) =>
            amount * (1f - Protection * math.saturate(intact)) * hit.GarrisonFactor;
    }

    /// <summary>A hit on a <see cref="Shelter"/> that reaches its passengers, waiting for the container to share it out.</summary>
    [InternalBufferCapacity(0)]
    public struct ShelterHit : IBufferElementData
    {
        /// <summary>The hit with the amount that got through; no target or splash.</summary>
        public DamageEvent Hit;
    }
}
