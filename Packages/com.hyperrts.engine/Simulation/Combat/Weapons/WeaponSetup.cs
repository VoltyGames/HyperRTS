using HyperRTS.Simulation.Combat.Targeting;
using HyperRTS.Simulation.Common.Authoring;
using System.Collections.Generic;
using HyperRTS.Simulation.Common.Combat;
using Unity.Collections;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Adds the components that let an entity attack, and fire missions when its primary fires only on order.</summary>
    public static class WeaponSetup
    {
        /// <summary>
        /// The weapons, primary first, with a pose each; build each with <see cref="WeaponMount.Create"/>.
        /// <paramref name="designations"/> (null for none) name the first weapons in order.
        /// </summary>
        public static void Add(EntityWriter writer, IReadOnlyList<WeaponMount> mounts, CombatStance stance,
            IReadOnlyList<FixedString32Bytes> designations = null)
        {
            writer.AddBuffer(mounts);
            var poses = writer.AddBuffer<WeaponPose>();
            poses.Resize(mounts.Count, NativeArrayOptions.ClearMemory);
            for (var i = 0; designations != null && i < designations.Count; i++)
            {
                poses.ElementAt(i).Designation = designations[i];
            }

            writer.Add(stance);
            writer.Add<AttackTarget>();
            writer.SetEnabled<AttackTarget>(false);
            if (mounts[0].Weapon.FiresOnOrder)
            {
                writer.Add<PointAim>();
                writer.SetEnabled<PointAim>(false);
            }
        }

        /// <summary>The muzzles of weapons that fire from several in turn; see <see cref="Weapon.FirstMuzzle"/>.</summary>
        public static void AddMuzzles(EntityWriter writer, IReadOnlyList<WeaponMuzzle> muzzles)
        {
            writer.AddBuffer(muzzles);
        }
    }
}
