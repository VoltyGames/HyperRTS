using Unity.Entities;
using Unity.NetCode;

namespace HyperRTS.Simulation.Power
{
    /// <summary>Per-player power from completed buildings; consumers go <see cref="Unpowered"/> while it is low.</summary>
    public struct PowerGrid : IComponentData
    {
        [GhostField] public float Produced;
        [GhostField] public float Consumed;

        public readonly bool IsLow => Consumed > Produced;
    }
}
