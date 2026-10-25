using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// One muzzle of a weapon that fires from several in turn, such as a launcher's tubes, in the shooter's local
    /// space. Each weapon's muzzles sit together from its <see cref="Weapon.FirstMuzzle"/>. Server only.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct WeaponMuzzle : IBufferElementData
    {
        public float3 Point;
    }
}
