using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>An effect played once where the entity appears (the dust of a collapse on a ruin, a fallen tree).</summary>
    public struct SpawnEffect : IComponentData
    {
        public UnityObjectRef<GameObject> Prefab;
        public bool Played;
    }
}
