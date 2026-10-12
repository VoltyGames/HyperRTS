using Unity.Entities;
using UnityEngine;

namespace HyperRTS.Simulation.Combat
{
    /// <summary>
    /// An effect played once where the entity appears (the dust of a collapse on a ruin, a fallen tree). Enabled
    /// until presentation plays it, then disabled; an entity that shows a result nobody saw happen spawns disabled.
    /// </summary>
    public struct SpawnEffect : IComponentData, IEnableableComponent
    {
        public UnityObjectRef<GameObject> Prefab;
    }
}
