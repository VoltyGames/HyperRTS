using Unity.Entities;

namespace HyperRTS.Simulation.Power
{
    /// <summary>Power a completed building adds to its owner's grid; negative draws from it.</summary>
    public struct PowerSupply : IComponentData
    {
        public float Amount;
    }
}
