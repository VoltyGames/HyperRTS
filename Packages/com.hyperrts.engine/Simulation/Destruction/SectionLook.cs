using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Destruction
{
    /// <summary>What this client has seen happen to a <see cref="Section"/>, at the same index; presentation keeps it.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionLook : IBufferElementData
    {
        /// <summary>The holed variant showing plus one, 0 for the standing model.</summary>
        public byte Hole;

        /// <summary>Where the latest seen impact landed, in the building's space.</summary>
        public float3 Impact;

        /// <summary>When it landed on the world clock, or minus infinity for none.</summary>
        public float ImpactTime;

        /// <summary>When its break clip ends on the world clock, while it is breaking.</summary>
        public float BreakEnd;

        public static SectionLook Untouched => new() { ImpactTime = float.NegativeInfinity };
    }
}
