using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// A replicated buffer element that names a prefab by <c>EntityInfo.TypeId</c>, since entities can't cross the
    /// network; clients fill the prefab back in (<c>PrefabReferences.Resolve</c>).
    /// </summary>
    public interface IPrefabReference : IBufferElementData
    {
        int TypeId { get; }

        Entity Prefab { get; set; }
    }
}
