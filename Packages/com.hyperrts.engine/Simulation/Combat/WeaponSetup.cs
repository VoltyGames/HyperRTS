using HyperRTS.Simulation.Common;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Adds the components that let an entity attack.</summary>
    public static class WeaponSetup
    {
        public static void Add<TSink>(ref TSink sink, in Weapon weapon, Stance stance, float3 anchor)
            where TSink : struct, IComponentSink
        {
            sink.Add(weapon);
            sink.Add(new CombatStance { Value = stance, Anchor = anchor });
            sink.Add<AttackTarget>();
            sink.SetEnabled<AttackTarget>(false);
        }

        /// <summary>Limits the weapon to <paramref name="rounds"/> shots, starting full.</summary>
        public static void AddAmmo<TSink>(ref TSink sink, int rounds, float reloadTime)
            where TSink : struct, IComponentSink
        {
            sink.Add(new Ammo { Max = rounds, Current = rounds, ReloadTime = reloadTime });
        }
    }
}
