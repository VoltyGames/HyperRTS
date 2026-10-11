using Unity.Entities;

namespace HyperRTS.Simulation.Animation
{
    /// <summary>Running time of a machine (pumpjack, turbine) that its <see cref="MachinePart"/>s move by.</summary>
    public struct MachineClock : IComponentData
    {
        public float Time;

        /// <summary>Stands still while neutral: a captured derrick starts pumping.</summary>
        public bool OwnedOnly;
    }
}
