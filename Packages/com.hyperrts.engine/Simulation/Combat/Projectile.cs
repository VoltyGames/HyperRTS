using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A shot in flight, homing on <see cref="Target"/> (or its last known position) and hitting on arrival.</summary>
    public struct Projectile : IComponentData
    {
        public Entity Target;
        public float3 TargetPosition;
        public float Speed;
        public float Damage;
        public UnityObjectRef<DamageType> DamageType;
    }
}
