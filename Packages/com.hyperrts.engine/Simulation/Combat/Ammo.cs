using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Rounds left in the weapon; refilled while docked.</summary>
    public struct Ammo : IComponentData
    {
        [GhostField] public int Max;
        [GhostField] public int Current;
        public float ReloadTime;
        public float ReloadElapsed;

        /// <summary>Weapons without ammo never run dry.</summary>
        public static bool IsEmpty(in ComponentLookup<Ammo> lookup, Entity entity) =>
            lookup.TryGetComponent(entity, out var ammo) && ammo.Current <= 0;
    }
}
