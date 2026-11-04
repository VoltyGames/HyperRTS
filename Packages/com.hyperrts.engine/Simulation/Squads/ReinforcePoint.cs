using Unity.Entities;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A place where friendly squads within <see cref="Range"/> of it can bring back dead members.</summary>
    public struct ReinforcePoint : IComponentData
    {
        public float Range;
    }

    /// <summary>
    /// A squad bringing back its dead members one at a time at <see cref="Point"/>. Each member is paid for when its
    /// turn starts and refunded if the reinforcing is cut short.
    /// </summary>
    public struct Reinforcing : IComponentData, IEnableableComponent
    {
        public Entity Point;

        /// <summary>The player entity whose resources pay.</summary>
        public Entity Payer;

        /// <summary>Seconds spent on the current member.</summary>
        public float Elapsed;

        public bool Paid;
    }
}
