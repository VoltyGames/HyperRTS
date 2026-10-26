using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>Rounds left in the weapon; <see cref="Refill"/> says where and how they come back.</summary>
    public struct Ammo : IComponentData
    {
        [GhostField] public int Max;
        [GhostField] public int Current;
        public float ReloadTime;
        public float ReloadElapsed;
        public AmmoRefill Refill;

        public void Reload(float deltaTime) =>
            Reload(ref Current, Max, ref ReloadElapsed, ReloadTime, Refill, deltaTime);

        /// <summary>
        /// Refills one round per <paramref name="reloadTime"/>, or with <see cref="AmmoRefill.Magazine"/> all of them
        /// once <paramref name="reloadTime"/> has passed since it ran empty. <see cref="WeaponMount"/> and
        /// <see cref="Interceptor"/> share it.
        /// </summary>
        public static void Reload(ref int rounds, int max, ref float elapsed, float reloadTime, AmmoRefill refill,
            float deltaTime)
        {
            if (!CanReload(rounds, max, refill))
            {
                elapsed = 0f;
                return;
            }

            elapsed += deltaTime;
            if (elapsed < reloadTime)
            {
                return;
            }

            if (refill == AmmoRefill.Magazine)
            {
                elapsed = 0f;
                rounds = max;
                return;
            }

            elapsed -= reloadTime;
            rounds++;
        }

        /// <summary>Weapons without ammo never run dry.</summary>
        public static bool IsEmpty(in ComponentLookup<Ammo> lookup, Entity entity) =>
            lookup.TryGetComponent(entity, out var ammo) && ammo.Current <= 0;

        private static bool CanReload(int rounds, int max, AmmoRefill refill)
        {
            if (rounds >= max)
            {
                return false;
            }

            return refill != AmmoRefill.Magazine || rounds <= 0;
        }
    }
}
