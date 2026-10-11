using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Enabled while an entity is out of action: its weapons, abilities and fields stop and production halts (low power
    /// only slows it). <see cref="Reasons"/> says why; each source sets and clears its own reason, and
    /// <c>OfflineSystem</c> keeps the enabled state in step.
    /// </summary>
    public struct Offline : IComponentData, IEnableableComponent
    {
        public OfflineReason Reasons;

        /// <summary>Offline for any reason other than <paramref name="ignored"/>.</summary>
        public readonly bool HasReasonOtherThan(OfflineReason ignored) => (Reasons & ~ignored) != 0;
    }
}
