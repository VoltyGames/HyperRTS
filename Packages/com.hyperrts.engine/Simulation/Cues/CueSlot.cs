namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// What an entity's sound and effect are for. Voices, <see cref="ProductionDone"/> and announcements reach only the
    /// owner (<see cref="CueRules.IsOwnerOnly"/>). <see cref="Impact"/> is where a shot landed: its sound, the shooter's
    /// impact effects and marks. Values from <see cref="GameFirst"/> up belong to games.
    /// </summary>
    public enum CueSlot : byte
    {
        None = 0,

        /// <summary>Each shot, at the muzzle.</summary>
        Fire = 1,
        Impact = 2,
        Death = 3,
        Ability = 4,

        /// <summary>Produced, or construction finished.</summary>
        Ready = 5,
        Select = 6,
        Move = 7,
        Attack = 8,

        /// <summary>Each shot of the first, second and third extra weapon mount, at its muzzle.</summary>
        Fire2 = 9,
        Fire3 = 10,
        Fire4 = 11,

        /// <summary>The caster's voice when it uses an ability, beside the world <see cref="Ability"/> sound.</summary>
        AbilityVoice = 12,

        /// <summary>Taking damage after a quiet spell, throttled per unit and per player.</summary>
        UnderFire = 13,

        /// <summary>At the producer when a unit or research in its queue finishes.</summary>
        ProductionDone = 14,

        /// <summary>
        /// Announcer alerts to the owner's player, from <see cref="AnnounceUnitLost"/> to
        /// <see cref="AnnounceUpgradeComplete"/>. The local <c>AnnouncerSet</c> picks the line, not the source's sounds.
        /// </summary>
        AnnounceUnitLost = 15,
        AnnounceBaseUnderAttack = 16,
        AnnounceLowPower = 17,
        AnnounceConstructionComplete = 18,
        AnnounceUnitReady = 19,
        AnnounceUpgradeComplete = 20,

        /// <summary>First value a game may use for its own slots; the engine never defines one from here up.</summary>
        GameFirst = 128,
    }
}
