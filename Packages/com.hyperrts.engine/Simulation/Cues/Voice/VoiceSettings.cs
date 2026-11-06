using System;
using UnityEngine;

namespace HyperRTS.Simulation.Cues.Voice
{
    /// <summary>
    /// A match's voice pacing as set on the Match; baked into <see cref="VoiceRules"/>. It sits on the match because
    /// each limit spans many units (a player's, a squad's), so no unit's sound set can own it.
    /// </summary>
    [Serializable]
    public struct VoiceSettings
    {
        [Tooltip("Seconds between two acknowledgements of the local player's orders, so spam-clicking doesn't stack voices.")]
        [Min(0f)]
        public float acknowledgementInterval;

        [Tooltip("Seconds between two under fire voices of one player.")]
        [Min(0f)]
        public float underFireInterval;

        [Tooltip("Seconds a unit must go without a hit before the next one makes it call out under fire again.")]
        [Min(0f)]
        public float underFireQuiet;

        [Tooltip("Seconds between two barks of one squad.")]
        [Min(0f)]
        public float squadBarkInterval;

        [Tooltip("Seconds between two barks of the local player's squads.")]
        [Min(0f)]
        public float squadBarkPlayerInterval;

        public static VoiceSettings Standard => new()
        {
            acknowledgementInterval = 1f, underFireInterval = 5f, underFireQuiet = 10f,
            squadBarkInterval = 6f, squadBarkPlayerInterval = 1.5f,
        };

        public readonly VoiceRules Rules => new()
        {
            AcknowledgementInterval = acknowledgementInterval,
            UnderFireInterval = underFireInterval,
            UnderFireQuiet = underFireQuiet,
            SquadBarkInterval = squadBarkInterval,
            SquadBarkPlayerInterval = squadBarkPlayerInterval,
        };
    }
}
