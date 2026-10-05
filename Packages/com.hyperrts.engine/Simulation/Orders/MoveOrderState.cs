using Unity.Entities;
using Unity.Mathematics;

namespace HyperRTS.Simulation.Orders
{
    /// <summary>
    /// Progress of a Move/AttackMove order. Enabled once the order has pointed locomotion at its goal; disabled
    /// whenever the order changes or combat takes over, so the goal is re-applied afterwards.
    /// </summary>
    public struct MoveOrderState : IComponentData, IEnableableComponent
    {
        /// <summary>Where the stall timer last restarted; the unit counts as stalled until it leaves this spot.</summary>
        public float3 StallAnchor;

        public float StallTime;
    }
}
