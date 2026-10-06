using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    [InternalBufferCapacity(2)]
    public struct ArmorModifier : IBufferElementData
    {
        public UnityObjectRef<DamageType> DamageType;
        public float Multiplier;
    }
}
