namespace HyperRTS.Simulation.Combat.Weapons
{
    /// <summary>Rounds left in a <see cref="WeaponMount"/>; <see cref="Refill"/> says where and how they come back.</summary>
    public struct Ammo
    {
        public int Max;
        public int Current;
        public float ReloadTime;
        public float ReloadElapsed;
        public AmmoRefill Refill;

        /// <summary>0 rounds max never runs dry.</summary>
        public bool Limited => Max > 0;

        public bool Empty => Limited && Current <= 0;

        public void Spend()
        {
            if (Limited)
            {
                Current--;
            }
        }

        public static Ammo Create(int rounds, float reloadTime, AmmoRefill refill) => new()
        {
            Max = rounds,
            Current = rounds,
            ReloadTime = reloadTime,
            Refill = refill,
        };

        /// <summary>
        /// Refills one round per <see cref="ReloadTime"/>, or with <see cref="AmmoRefill.Magazine"/> all of them once
        /// <see cref="ReloadTime"/> has passed since it ran empty.
        /// </summary>
        public void Reload(float deltaTime)
        {
            if (!CanReload())
            {
                ReloadElapsed = 0f;
                return;
            }

            ReloadElapsed += deltaTime;
            if (ReloadElapsed < ReloadTime)
            {
                return;
            }

            if (Refill == AmmoRefill.Magazine)
            {
                ReloadElapsed = 0f;
                Current = Max;
                return;
            }

            ReloadElapsed -= ReloadTime;
            Current++;
        }

        private bool CanReload() => Current < Max && (Refill != AmmoRefill.Magazine || Current <= 0);
    }
}
