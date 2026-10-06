using Unity.Entities;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>A shot in flight, homing on its hit's target (or its last known position) and hitting on arrival.</summary>
    public struct Projectile : IComponentData
    {
        public float Speed;

        /// <summary>Queued on impact; its Position is the aim point, tracked while the target lives.</summary>
        public DamageEvent Hit;
    }
}
