using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary>
    /// Singleton: <see cref="EntityInfo.TypeId"/> to prefab for every loaded prefab, for code that receives prefabs as
    /// type ids (network commands, replicated buffers, sounds). Kept current by <see cref="PrefabRegistrySystem"/>.
    /// </summary>
    public struct PrefabRegistry : IComponentData
    {
        public NativeHashMap<int, Entity> ByTypeId;

        /// <summary>The prefab of <paramref name="typeId"/>, or <c>Entity.Null</c> when none has loaded.</summary>
        public readonly Entity Find(int typeId) => ByTypeId.TryGetValue(typeId, out var prefab) ? prefab : Entity.Null;
    }
}
