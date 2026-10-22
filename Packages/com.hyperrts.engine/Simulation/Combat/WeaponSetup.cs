using System.Collections.Generic;
using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds the components that let an entity attack.</summary>
    public static class WeaponSetup
    {
        /// <summary>The primary weapon, with poses for it and <paramref name="extraPoses"/> mounts and interceptors.</summary>
        public static void Add<TWriter>(ref TWriter writer, in Weapon weapon, Stance stance, float3 returnPoint,
            int extraPoses) where TWriter : struct, IEntityWriter
        {
            writer.Add(weapon);
            writer.AddBuffer<WeaponPose>().Resize(1 + extraPoses, NativeArrayOptions.ClearMemory);
            writer.Add(new CombatStance { Value = stance, ReturnPoint = returnPoint });
            writer.Add<AttackTarget>();
            writer.SetEnabled<AttackTarget>(false);
        }

        /// <summary>Limits the weapon to <paramref name="rounds"/> shots, starting full.</summary>
        public static void AddAmmo<TWriter>(ref TWriter writer, int rounds, float reloadTime)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Ammo { Max = rounds, Current = rounds, ReloadTime = reloadTime });
        }

        /// <summary>Extra weapons beside the primary; build each with <see cref="WeaponMount.Create"/>.</summary>
        public static void AddMounts<TWriter>(ref TWriter writer, IReadOnlyList<WeaponMount> mounts)
            where TWriter : struct, IEntityWriter
        {
            writer.AddBuffer(mounts);
        }
    }
}
