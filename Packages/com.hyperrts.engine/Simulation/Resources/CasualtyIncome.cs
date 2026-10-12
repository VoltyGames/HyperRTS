using Unity.Entities;

namespace HyperRTS.Simulation.Resources
{
    /// <summary>
    /// While owned and not <c>Offline</c>, pays the owner <see cref="Share"/> of the <see cref="Type"/> cost of every
    /// unit that dies belonging to the owner or a hostile faction (rounded down).
    /// </summary>
    public struct CasualtyIncome : IComponentData
    {
        public UnityObjectRef<ResourceType> Type;
        public float Share;
    }
}
