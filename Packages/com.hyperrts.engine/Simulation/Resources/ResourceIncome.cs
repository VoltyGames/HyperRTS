using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// Pays the owner a fixed amount every interval while it holds this entity (an oil derrick), plus a one-time
    /// bonus to the first player to own it. Neutral entities pay nothing.
    /// </summary>
    public struct ResourceIncome : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
        public float Interval;
        public int FirstOwnerBonus;
        public float Elapsed;
        public bool BonusPaid;
    }
}
