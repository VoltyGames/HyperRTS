using Unity.Entities;

namespace HyperRTS.Simulation.Match
{
    /// <summary>Match-wide tuning on the match singleton; systems fall back to <see cref="Default"/> without one.</summary>
    public struct MatchRules : IComponentData
    {
        /// <summary>Production speed of an <c>Unpowered</c> producer.</summary>
        public float LowPowerProductionRate;

        /// <summary>Share of a finished building's cost refunded when sold; unfinished ones refund in full.</summary>
        public float SellRefund;

        /// <summary>Record a replay from the start of the match (<c>ReplayRecorderSystem</c>).</summary>
        public bool RecordReplay;

        public static MatchRules Default => new() { LowPowerProductionRate = 0.5f, SellRefund = 0.5f };
    }
}
