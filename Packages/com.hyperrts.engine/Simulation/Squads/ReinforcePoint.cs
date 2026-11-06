using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Squads
{
    /// <summary>A place where friendly squads within <see cref="Range"/> of it can bring back dead members.</summary>
    public struct ReinforcePoint : IComponentData, ISquadPoint
    {
        public float Range;

        public readonly float Reach => Range;
    }

    /// <summary>
    /// A squad bringing back its dead members one at a time at <see cref="Point"/>. Each member is paid for when its
    /// turn starts and refunded if the reinforcing is cut short. Clients see that it is on and how far the current
    /// member is.
    /// </summary>
    [GhostEnabledBit]
    public struct Reinforcing : IComponentData, IEnableableComponent
    {
        [GhostField(SendData = false)] public Entity Point;

        /// <summary>The player entity whose resources pay.</summary>
        [GhostField(SendData = false)] public Entity Payer;

        /// <summary>Seconds spent on the current member.</summary>
        [GhostField(Quantization = 100)] public float Elapsed;

        [GhostField(SendData = false)] public bool Paid;
    }
}
