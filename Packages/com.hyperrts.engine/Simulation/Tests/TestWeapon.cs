using HyperRTS.Simulation.Combat.Projectiles;
using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common.Combat;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The primary weapon <see cref="CombatTestKit.AddWeapon(TestWorld, Entity, TestWeapon)"/> gives an entity.</summary>
    public sealed class TestWeapon
    {
        public Stance Stance = Stance.Aggressive;
        public float Range = 4f;
        public float Damage = 25f;
        public float Cooldown = 0.5f;
        public Entity Projectile;
        public UnityObjectRef<DamageType> DamageType;
        public WeaponTargets Targets = WeaponTargets.Surface;
        public Trajectory Trajectory = Trajectory.Direct;
        public float ArcHeight;
        public float MinRange;
        public bool Turreted;
        public float TraverseSpeed = 360f;
    }
}
