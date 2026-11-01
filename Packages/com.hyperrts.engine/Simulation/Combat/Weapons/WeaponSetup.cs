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
        /// <summary>The weapons, primary first, with a pose each; build each with <see cref="WeaponMount.Create"/>.</summary>
        public static void Add(EntityWriter writer, IReadOnlyList<WeaponMount> mounts, Stance stance,
            float3 returnPoint)
        {
            writer.AddBuffer(mounts);
            writer.AddBuffer<WeaponPose>().Resize(mounts.Count, NativeArrayOptions.ClearMemory);
            writer.Add(new CombatStance { Value = stance, ReturnPoint = returnPoint });
            writer.Add<AttackTarget>();
            writer.SetEnabled<AttackTarget>(false);
        }

        /// <summary>The muzzles of weapons that fire from several in turn; see <see cref="Weapon.FirstMuzzle"/>.</summary>
        public static void AddMuzzles(EntityWriter writer, IReadOnlyList<WeaponMuzzle> muzzles)
        {
            writer.AddBuffer(muzzles);
        }
    }
}
