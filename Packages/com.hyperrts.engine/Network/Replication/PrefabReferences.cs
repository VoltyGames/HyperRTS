using HyperRTS.Simulation.Common;
using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Network.Replication
{
    /// <summary>Fills in the prefabs of replicated <see cref="IPrefabReference"/> buffers from the client's own prefabs.</summary>
    public static class PrefabReferences
    {
        /// <summary>A query for every <typeparamref name="T"/> buffer a snapshot changed since the system last ran.</summary>
        public static EntityQuery Changed<T>(ref SystemState state) where T : unmanaged, IPrefabReference
        {
            var query = state.GetEntityQuery(ComponentType.ReadWrite<T>());
            query.SetChangedVersionFilter(ComponentType.ReadWrite<T>());
            return query;
        }

        public static void Resolve<T>(EntityManager entityManager, EntityQuery changed, in PrefabRegistry prefabs)
            where T : unmanaged, IPrefabReference
        {
            foreach (var entity in changed.ToEntityArray(Allocator.Temp))
            {
                var buffer = entityManager.GetBuffer<T>(entity);
                for (var i = 0; i < buffer.Length; i++)
                {
                    var element = buffer[i];
                    element.Prefab = prefabs.Find(element.TypeId);
                    buffer[i] = element;
                }
            }
        }
    }
}
