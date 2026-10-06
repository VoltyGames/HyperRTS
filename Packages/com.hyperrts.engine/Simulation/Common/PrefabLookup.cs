using Unity.Collections;
using Unity.Entities;

namespace HyperRTS.Simulation.Common
{
    /// <summary><see cref="EntityInfo.TypeId"/> to prefab, for code that receives prefabs as type ids.</summary>
    public static class PrefabLookup
    {
        /// <summary><paramref name="prefabs"/> must match <see cref="EntityInfo"/> and <see cref="Prefab"/>.</summary>
        public static void ByTypeId(EntityQuery prefabs, NativeHashMap<int, Entity> result)
        {
            result.Clear();
            var entities = prefabs.ToEntityArray(Allocator.Temp);
            var data = prefabs.ToComponentDataArray<EntityInfo>(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                result[data[i].TypeId] = entities[i];
            }
        }
    }
}
