using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Match-wide tuning on the match singleton, baked by <c>MatchAuthoring</c>; systems that read it require it.</summary>
    public struct MatchRules : IComponentData
    {
        /// <summary>Production speed of an unpowered producer (<c>OfflineReason.Unpowered</c>).</summary>
        public float LowPowerProductionRate;

        /// <summary>Share of a finished building's cost refunded when sold; unfinished ones refund in full.</summary>
        public float SellRefund;

        /// <summary>Experience a kill pays per resource unit of the victim's cost (<c>KillCreditSystem</c>).</summary>
        public float ExperiencePerCost;

        /// <summary>Record a replay from the start of the match (<c>ReplayRecorderSystem</c>).</summary>
        public bool RecordReplay;

        /// <summary>Networked matches: seconds the server waits for every human slot to join before starting anyway.</summary>
        public float JoinTimeout;

        /// <summary>Strategic assets charge and fire; off holds every charge (ranked 1v1 by default).</summary>
        public bool StrategicAssets;
    }
}
