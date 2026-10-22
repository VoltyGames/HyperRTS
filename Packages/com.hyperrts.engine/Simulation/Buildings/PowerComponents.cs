using HyperRTS.Simulation.Common;
using Unity.Entities;
using Unity.Netcode;

namespace HyperRTS.Simulation.Buildings
{
    /// <summary>
    /// Per-player power from completed buildings; consumers go offline (<see cref="OfflineReason.Unpowered"/>) while it
    /// is low.
    /// </summary>
    public struct PowerGrid : IComponentData
    {
        [GhostField] public float Produced;
        [GhostField] public float Consumed;

        public readonly bool IsLow => Consumed > Produced;
    }

    /// <summary>Power a completed building adds to its owner's grid; negative draws from it.</summary>
    public struct PowerSupply : IComponentData
    {
        public float Amount;
    }
}
