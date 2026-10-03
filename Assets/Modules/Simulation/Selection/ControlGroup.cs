using Unity.Entities;

namespace HyperRTS.Simulation.Selection
{
    /// <summary>Control-group membership (bit n = group n); added the first time an entity is assigned.</summary>
    public struct ControlGroup : IComponentData
    {
        public byte Mask;
    }
}
