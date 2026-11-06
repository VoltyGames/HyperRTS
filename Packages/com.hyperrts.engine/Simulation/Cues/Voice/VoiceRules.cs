using Unity.Entities;

namespace HyperRTS.Simulation.Cues.Voice
{
    /// <summary>On the match: how often units speak. Baked from <c>VoiceSettings</c>.</summary>
    public struct VoiceRules : IComponentData
    {
        /// <summary>Seconds between two acknowledgements of the local player's orders.</summary>
        public float AcknowledgementInterval;

        /// <summary>Seconds between two under fire voices of one player.</summary>
        public float UnderFireInterval;

        /// <summary>Seconds without a hit before the next one makes a unit call out again.</summary>
        public float UnderFireQuiet;

        /// <summary>Seconds between two barks of one squad.</summary>
        public float SquadBarkInterval;

        /// <summary>Seconds between two barks of the local player's squads.</summary>
        public float SquadBarkPlayerInterval;
    }
}
