using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Adds the components that let an entity attack.</summary>
    public static class WeaponSetup
    {
        /// <summary>The primary weapon, with poses for it and <paramref name="extraPoses"/> mounts and interceptors.</summary>
        public static void Add(EntityWriter writer, in Weapon weapon, Stance stance, float3 returnPoint,
            int extraPoses)
        {
            writer.Add(weapon);
            writer.AddBuffer<WeaponPose>().Resize(1 + extraPoses, NativeArrayOptions.ClearMemory);
            writer.Add(new CombatStance { Value = stance, ReturnPoint = returnPoint });
            writer.Add<AttackTarget>();
            writer.SetEnabled<AttackTarget>(false);
        }

        /// <summary>Limits the weapon to <paramref name="rounds"/> shots, starting full.</summary>
        public static void AddAmmo(EntityWriter writer, int rounds, float reloadTime, AmmoRefill refill)
        {
            writer.Add(Ammo.Create(rounds, reloadTime, refill));
        }

        /// <summary>The muzzles of weapons that fire from several in turn; see <see cref="Weapon.FirstMuzzle"/>.</summary>
        public static void AddMuzzles(EntityWriter writer, IReadOnlyList<WeaponMuzzle> muzzles)
        {
            writer.AddBuffer(muzzles);
        }

        /// <summary>Extra weapons beside the primary; build each with <see cref="WeaponMount.Create"/>.</summary>
        public static void AddMounts(EntityWriter writer, IReadOnlyList<WeaponMount> mounts)
        {
            writer.AddBuffer(mounts);
        }
    }
}
