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

        /// <summary>Deterministic FNV-1a hash, stable across runs and machines.</summary>
        public static int TypeIdFromName(string name)
        {
            var hash = 2166136261u;
            foreach (var c in name)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return (int)hash;
        }
    }
}
