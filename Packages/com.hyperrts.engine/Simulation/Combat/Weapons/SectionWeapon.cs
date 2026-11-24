using HyperRTS.Simulation.Destruction;
using Unity.Entities;

namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>A weapon mount that stays silent while section <see cref="Section"/> is broken.</summary>
    [InternalBufferCapacity(0)]
    public struct SectionWeapon : IBufferElementData
    {
        /// <summary>Index of the section that carries it.</summary>
        public byte Section;

        /// <summary>Index into the building's <see cref="WeaponMount"/> buffer.</summary>
        public byte Mount;

        /// <summary>Silences each mount whose section is in <paramref name="broken"/> and frees the others.</summary>
        public static void Apply(DynamicBuffer<SectionWeapon> weapons, ulong broken, DynamicBuffer<WeaponMount> mounts)
        {
            for (var m = 0; m < mounts.Length; m++)
            {
                var silenced = false;
                foreach (var weapon in weapons)
                {
                    silenced |= weapon.Mount == m && (broken & (1ul << weapon.Section)) != 0ul;
                }

                if (mounts[m].Silenced != silenced)
                {
                    var mount = mounts[m];
                    mount.Silenced = silenced;
                    mounts[m] = mount;
                }
            }
        }
    }
}
