using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Common
{
    /// <summary>Type identity and display data. <see cref="TypeId"/> groups instances of one prefab.</summary>
    public struct EntityInfo : IComponentData
    {
        public int TypeId;
        public FixedString64Bytes Name;
        public UnityObjectRef<Texture2D> Icon;

        /// <summary>The <see cref="TypeId"/> of <paramref name="entity"/>, or 0 when it has no <see cref="EntityInfo"/>.</summary>
        public static int TypeIdOf(EntityManager entityManager, Entity entity) =>
            entityManager.HasComponent<EntityInfo>(entity) ? entityManager.GetComponentData<EntityInfo>(entity).TypeId : 0;

        public static int TypeIdFromName(string name) => (int)Fnv1a(name);

        /// <summary>Deterministic FNV-1a hash over UTF-16 code units, stable across runs and machines.</summary>
        public static uint Fnv1a(string text)
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }
    }
}
