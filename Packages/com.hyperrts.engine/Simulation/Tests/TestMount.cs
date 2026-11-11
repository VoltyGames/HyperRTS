using HyperRTS.Simulation.Combat.Weapons;
using HyperRTS.Simulation.Common;
using Unity.Entities;

namespace HyperRTS.Simulation.Tests
{
    /// <summary>The extra weapon <see cref="CombatTestKit.AddMount(TestWorld, Entity, TestMount)"/> gives an entity.</summary>
    public sealed class TestMount
    {
        public float Range = 4f;
        public float Damage = 10f;
        public float Cooldown = 0.5f;
        public WeaponTargets Targets = WeaponTargets.Surface;
        public bool Independent;
        public float MinEffect;
        public UnityObjectRef<DamageType> DamageType;
        public int Rounds;
        public float ReloadTime = 0.5f;
        public int RequiredResearchTypeId;
        public AmmoRefill Refill = AmmoRefill.Docked;
    }
}
