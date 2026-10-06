using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds the components that let an entity attack.</summary>
    public static class WeaponSetup
    {
        public static void Add<TWriter>(ref TWriter writer, in Weapon weapon, Stance stance, float3 anchor)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(weapon);
            writer.Add(new CombatStance { Value = stance, Anchor = anchor });
            writer.Add<AttackTarget>();
            writer.SetEnabled<AttackTarget>(false);
        }

        /// <summary>Limits the weapon to <paramref name="rounds"/> shots, starting full.</summary>
        public static void AddAmmo<TWriter>(ref TWriter writer, int rounds, float reloadTime)
            where TWriter : struct, IEntityWriter
        {
            writer.Add(new Ammo { Max = rounds, Current = rounds, ReloadTime = reloadTime });
        }
    }
}
