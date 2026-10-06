using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// One hit waiting for <see cref="DamageSystem"/>: full damage to <see cref="Target"/>, plus falloff splash around
    /// <see cref="Position"/> when <see cref="Radius"/> is set. A negative amount heals.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct DamageEvent : IBufferElementData
    {
        /// <summary>Takes full damage; <c>Entity.Null</c> for a pure area hit.</summary>
        public Entity Target;

        public float3 Position;

        /// <summary>Where the hit came from; directional armor compares it with the target's facing.</summary>
        public float3 Origin;

        public Entity Source;
        public byte SourceFaction;
        public float Amount;
        public UnityObjectRef<DamageType> Type;
        public float Radius;

        /// <summary>Fraction of <see cref="Amount"/> dealt at the splash edge, 1 = no falloff.</summary>
        public float EdgeFactor;

        /// <summary>Splash also hurts the source's allies.</summary>
        public bool FriendlyFire;

        /// <summary>Layers the splash reaches: the weapon's targets, else the surface only.</summary>
        public WeaponTargets Reach;
    }

    /// <summary>Tags the singleton holding this frame's <see cref="DamageEvent"/>s.</summary>
    public struct DamageQueue : IComponentData { }
}
