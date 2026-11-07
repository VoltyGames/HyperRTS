namespace HyperRTS.Simulation.Cues
{
    /// <summary>
    /// What an entity's sound and effect are for. Voices, <see cref="ProductionDone"/> and announcements reach only the owner (<see cref="CueRules.IsOwnerOnly"/>).
    /// Values from <see cref="GameFirst"/> up belong to games.
    /// </summary>
    public enum CueSlot : byte
    {
        None = 0,

        /// <summary>Each shot, at the muzzle; <see cref="CueEvent.Variant"/> is the weapon's pose index, 0 the primary.</summary>
        Fire = 1,
        Impact = 2,
        Death = 3,
        Ability = 4,

        /// <summary>Produced, or construction finished.</summary>
        Ready = 5,
        Select = 6,
        Move = 7,
        Attack = 8,

        /// <summary>The caster's voice when it uses an ability, beside the world <see cref="Ability"/> sound.</summary>
        AbilityVoice = 9,

        /// <summary>Taking damage after a quiet spell, throttled per unit and per player.</summary>
        UnderFire = 10,

        /// <summary>At the producer when a unit or research in its queue finishes.</summary>
        ProductionDone = 11,

        /// <summary>
        /// An announcer alert to the owner's player, its <see cref="AnnouncementKind"/> in
        /// <see cref="CueEvent.Variant"/>. The local <c>AnnouncerSet</c> picks the line, not the source's sounds.
        /// </summary>
        Announce = 12,

        /// <summary>A halted squad's members reach cover.</summary>
        TakeCover = 13,

        /// <summary>A squad's suppression rises to suppressed.</summary>
        Suppressed = 14,

        /// <summary>A squad's suppression rises to pinned.</summary>
        Pinned = 15,

        /// <summary>A squad starts to retreat.</summary>
        Retreat = 16,

        /// <summary>A squad's members start digging.</summary>
        DigIn = 17,

        /// <summary>A squad gets a dead member back.</summary>
        Reinforced = 18,

        /// <summary>First value a game may use for its own slots; the engine never defines one from here up.</summary>
        GameFirst = 128,
    }
}
