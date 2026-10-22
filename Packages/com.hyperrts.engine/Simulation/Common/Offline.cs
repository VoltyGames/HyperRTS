using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Enabled while an entity is out of action: its weapons, abilities and fields stop and production halts (low power
    /// only slows it). <see cref="Reasons"/> says why; each source sets and clears its own reason with <see cref="Set"/>,
    /// and <c>OfflineSystem</c> keeps the enabled state in step.
    /// </summary>
    [GhostEnabledBit]
    public struct Offline : IComponentData, IEnableableComponent
    {
        /// <summary>Replicated so clients can stop machines and dim lights on low power.</summary>
        [GhostField] public OfflineReason Reasons;

        /// <summary>Offline for any reason other than <paramref name="ignored"/>.</summary>
        public readonly bool HasReasonOtherThan(OfflineReason ignored) => (Reasons & ~ignored) != 0;

        /// <summary>Adds or clears <paramref name="reason"/>, leaving every other source's reasons alone.</summary>
        public void Set(OfflineReason reason, bool holds)
        {
            Reasons &= ~reason;
            if (holds)
            {
                Reasons |= reason;
            }
        }
    }
}
