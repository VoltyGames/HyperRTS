using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// One part of a building that breaks on its own (a wall panel, a roof panel, a floor), as a box in the
    /// building's space. Its index is its bit in the <see cref="Sections"/> masks.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct Section : IBufferElementData
    {
        public float3 Center;
        public float3 HalfSize;
        public float Health;
        public float MaxHealth;

        /// <summary>Index into the building's <see cref="SectionFactor"/> materials.</summary>
        public byte Material;

        /// <summary>The storey it belongs to, 0 for the ground floor.</summary>
        public byte Floor;

        /// <summary>Bits of the sections it rests on.</summary>
        public ulong Supports;

        /// <summary>Stands on the ground, so it holds up whatever rests on it.</summary>
        public bool Anchored;

        /// <summary>An outer wall, which shelters a garrison.</summary>
        public bool Exterior;

        /// <summary>Horizontal, or up for a roof over the centre; the way debris flies.</summary>
        public float3 Outward;

        /// <summary>0 while it stands, else its place in the break order, so repair restores the latest.</summary>
        public int BreakOrder;

        public readonly bool IsBroken => BreakOrder > 0;

        /// <summary>Distance from <paramref name="local"/> to the box, 0 inside it.</summary>
        public readonly float Gap(float3 local) => math.length(math.max(math.abs(local - Center) - HalfSize, 0f));

        /// <summary>Where along the segment from <paramref name="from"/> to <paramref name="to"/> it enters the box, 0 to 1.</summary>
        public readonly bool Enters(float3 from, float3 to, out float along)
        {
            var delta = to - from;
            var inverse = math.select(1f / delta, float.MaxValue, math.abs(delta) < 1e-6f);
            var near = (Center - HalfSize - from) * inverse;
            var far = (Center + HalfSize - from) * inverse;
            var enter = math.cmax(math.min(near, far));
            var exit = math.cmin(math.max(near, far));
            along = math.max(enter, 0f);
            return enter <= exit && exit >= 0f && enter <= 1f;
        }
    }

    /// <summary>A damage multiplier for one of a building's section materials against one damage type.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionFactor : IBufferElementData
    {
        public byte Material;
        public UnityObjectRef<DamageType> DamageType;
        public float Multiplier;

        /// <summary>The multiplier for <paramref name="material"/> against <paramref name="type"/>, 1 when unlisted.</summary>
        public static float Find(DynamicBuffer<SectionFactor> factors, byte material, UnityObjectRef<DamageType> type)
        {
            foreach (var factor in factors)
            {
                if (factor.Material == material && factor.DamageType.Equals(type))
                {
                    return factor.Multiplier;
                }
            }

            return 1f;
        }
    }
}
