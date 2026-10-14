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

    /// <summary>Makes <see cref="ResourceIncome"/> pay its amount once per passenger aboard; empty, it pays nothing.</summary>
    public struct OccupantIncome : IComponentData { }

    /// <summary>
    /// While owned and not <c>Offline</c>, pays the owner <see cref="Share"/> of the <see cref="Type"/> cost of every
    /// unit that dies belonging to the owner or a hostile faction (rounded down).
    /// </summary>
    public struct CasualtyIncome : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        public float Share;
    }

    /// <summary>
    /// Finite stock a <see cref="ResourceIncome"/> pays out of. At zero it pays nothing but stays, so a
    /// <see cref="ReserveRefill"/> can top it up again.
    /// </summary>
    public struct ResourceReserve : IComponentData
    {
        public int Remaining;
        public int Capacity;
    }

    /// <summary>
    /// While owned and not <c>Offline</c>, adds <see cref="Amount"/> every interval to each same-owner
    /// <see cref="ResourceReserve"/> whose income pays <see cref="Type"/>, up to its capacity.
    /// </summary>
    public struct ReserveRefill : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        public int Amount;
        public float Interval;
        public float Elapsed;
    }
}
